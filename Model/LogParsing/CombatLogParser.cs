using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.LogParsing
{

    public static class CombatLogParser
    {
        private static Encoding _fileEncoding;

        public static void SetParseDate(DateTime logDate)
        {
            _7_0LogParsing.SetStartDate(logDate);
            _fileEncoding = Encoding.GetEncoding(1252);
        }
        public static ParsedLogEntry ParseLine(ReadOnlySpan<char> logEntry, long lineIndex, DateTime previousLogTime, bool realTime = true)
        {
            try
            {
                var listEntries = GetInfoComponents(logEntry);

                return _7_0LogParsing.ParseLog(logEntry, previousLogTime, lineIndex, listEntries, realTime);

            }
            catch (Exception e)
            {
                Logging.LogError("Log parsing error: ++" + logEntry.ToString() + "++\r\n" + JsonConvert.SerializeObject(e));
                return new ParsedLogEntry() { LogBytes = _fileEncoding.GetByteCount(logEntry), Error = ErrorType.IncompleteLine };
            }
        }
        private static bool GetAllLines(StreamReader sr, List<string> lines)
        {
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                lines.Add(line);
            }
            // 2) Peek at the very last byte on disk
            bool endedWithNewline = false;
            if (sr.BaseStream is FileStream fs)
            {
                long prevPos = fs.Position;
                long length  = fs.Length;

                if (length > 0)
                {
                    fs.Seek(length - 1, SeekOrigin.Begin);
                    endedWithNewline = fs.ReadByte() == '\n';
                }

                // restore reader to its previous state
                fs.Seek(prevPos, SeekOrigin.Begin);
                sr.DiscardBufferedData();
            }

            return endedWithNewline;
        }
        public static List<string> ExtractSpecificLines(CombatLogFile combatLog, int startLog, int endLog, bool includeAreaEntered = true)
        {
            var logLines = new List<string>();
            var worked = GetAllLines(combatLog.Data, logLines);
            var areaEnteredId = "836045448953664";
            var areaEnteredLog = "";

            var validLines = new List<string>();
            for (int i = startLog; i >= 0; i--)
            {
                if (logLines[i].Contains(areaEnteredId))
                {
                    areaEnteredLog = logLines[i];
                    break;
                }
            }
            validLines.Add(areaEnteredLog);
            for (int i = startLog; i <= endLog; i++)
            {
                validLines.Add(logLines[i]);
            }
            return validLines;
        }
        public static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog, bool includeIncomplete = false)
        {
            CombatLogStateBuilder.ClearState();

            var logLines = new List<string>();
            var worked = GetAllLines(combatLog.Data, logLines);
            if (!worked)
            {
                logLines = logLines.Take(logLines.Count - 1).ToList();
            }
            var numberOfLines = logLines.Count;
            ParsedLogEntry[] parsedLog = new ParsedLogEntry[numberOfLines];
            ConcurrentBag<ParsedLogEntry> incompleteLines = new ConcurrentBag<ParsedLogEntry>();
            Parallel.For(0, numberOfLines, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {

                if (logLines[i] == "")
                    return;
                var parsedLine = ParseLine(logLines[i], i, DateTime.MinValue, false);

                if (parsedLine.Error == ErrorType.IncompleteLine)
                {
                    incompleteLines.Add(parsedLine);
                    return;
                }
                parsedLog[i] = parsedLine;
                parsedLog[i].LogName = combatLog.Name;

            });

            var cleanedLogs = parsedLog.Where(l => l != null);
            CombatTimestampRectifier.RectifyTimeStamps(cleanedLogs.ToList());
            var orderdedLog = cleanedLogs.OrderBy(l => l.TimeStamp);
            UpdateStateAndLogs(orderdedLog.ToList(), false);
            if (includeIncomplete)
            {
                var includedLines = orderdedLog.ToList();
                includedLines.AddRange(incompleteLines);
                orderdedLog = includedLines.OrderBy(l => l.TimeStamp);
            }
            return orderdedLog.ToList();
        }
        private static List<string> GetInfoComponents(ReadOnlySpan<char> span)
        {
            var results = new List<string>();
            int depth = 0, start = -1;

            for (int i = 0; i < span.Length; i++)
            {
                char c = span[i];
                if (c == '[')
                {
                    if (depth++ == 0) start = i + 1;
                }
                else if (c == ']' && depth-- == 1)
                {
                    results.Add(span.Slice(start, i - start).ToString());
                }
            }

            return results;
        }

        private static void UpdateStateAndLogs(List<ParsedLogEntry> orderdedLog, bool realTime)
        {
            foreach (var line in orderdedLog)
            {
                CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(line, realTime);
            }
        }
    }
}
