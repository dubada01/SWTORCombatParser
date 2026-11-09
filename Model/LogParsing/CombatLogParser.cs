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

            var combatStartId = "836045448945489";
            var combatStartLog = "";
            var combatStartIndex = 0;

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
            for (int i = startLog; i >= 0; i--)
            {
                if (logLines[i].Contains(combatStartId))
                {
                    combatStartLog = logLines[i];
                    combatStartIndex = i;
                    break;
                }
            }
            validLines.Add(combatStartLog);
            for (int i = combatStartIndex + 1; i < endLog; i++)
            {
                if(logLines.Count > i)
                    validLines.Add(logLines[i]);
            }
            return validLines;
        }
        public static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog, bool includeIncomplete = false)
        {
            return ParseAllLines(combatLog, includeIncomplete, out _, out _);
        }

        public static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog, bool includeIncomplete, out long completedBytes, out int physicalLines, bool deferIncompleteTail = false)
        {
            CombatLogStateBuilder.ClearState();
            _7_0LogParsing.ResetEntities();

            var logLines = ReadCompletedLines(combatLog.Data, out completedBytes, out var lastLineStart);
            physicalLines = logLines.Count;
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

            // A live writer may temporarily terminate an unfinished last record.
            // Leave it for the live reader, which can retry it if it is repaired.
            if (deferIncompleteTail && numberOfLines > 0 &&
                !string.IsNullOrWhiteSpace(logLines[numberOfLines - 1]) && parsedLog[numberOfLines - 1] == null)
            {
                completedBytes = lastLineStart;
                physicalLines--;
            }

            var cleanedLogs = parsedLog.Where(l => l != null).ToList();
            CombatTimestampRectifier.RectifyTimeStamps(cleanedLogs);
            // Keep the stable file order for equal timestamps: apply/remove events
            // at the same instant must update state in their original order.
            var orderedLogs = cleanedLogs.OrderBy(l => l.TimeStamp).ToList();
            UpdateStateAndLogs(orderedLogs, false);
            if (includeIncomplete && !incompleteLines.IsEmpty)
            {
                orderedLogs.AddRange(incompleteLines);
                orderedLogs = orderedLogs.OrderBy(l => l.TimeStamp).ToList();
            }
            return orderedLogs;
        }
        private static List<string> ReadCompletedLines(StreamReader reader, out long completedBytes, out long lastLineStart)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            var buffer = new char[4096];
            completedBytes = 0;
            lastLineStart = 0;
            // Peek initializes encoding detection even for a file containing only a BOM.
            reader.Peek();
            var preamble = reader.CurrentEncoding.GetPreamble();
            if (preamble.Length > 0 && reader.BaseStream.CanSeek)
            {
                long position = reader.BaseStream.Position;
                reader.BaseStream.Seek(0, SeekOrigin.Begin);
                var bytes = new byte[preamble.Length];
                int read = reader.BaseStream.Read(bytes, 0, bytes.Length);
                reader.BaseStream.Seek(position, SeekOrigin.Begin);
                if (read == preamble.Length && bytes.SequenceEqual(preamble))
                    completedBytes = preamble.Length;
            }
            var encoding = reader.CurrentEncoding;
            var newlineBytes = encoding.GetByteCount("\n");
            var carriageReturnBytes = encoding.GetByteCount("\r");
            int count;
            while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                var remaining = buffer.AsSpan(0, count);
                while (!remaining.IsEmpty)
                {
                    var newlineIndex = remaining.IndexOf('\n');
                    if (newlineIndex < 0)
                    {
                        line.Append(remaining);
                        break;
                    }

                    line.Append(remaining.Slice(0, newlineIndex));
                    lastLineStart = completedBytes;
                    var hasCarriageReturn = line.Length > 0 && line[line.Length - 1] == '\r';
                    if (hasCarriageReturn)
                        line.Length--;
                    var completedLine = line.ToString();
                    completedBytes += encoding.GetByteCount(completedLine) + newlineBytes +
                        (hasCarriageReturn ? carriageReturnBytes : 0);
                    lines.Add(completedLine);
                    line.Clear();
                    remaining = remaining.Slice(newlineIndex + 1);
                }
            }
            return lines;
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
