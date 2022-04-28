using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SWTORCombatParser
{

    public static class CombatLogParser
    {
        private static DateTime _logDate;
        private static LogState _logState = new LogState();
        private static ConcurrentDictionary<string,Entity> _currentEntities = new ConcurrentDictionary<string,Entity>();
        private static Random _idGenerator = new Random();
        public static event Action<string> OnNewLog = delegate { };

        public static void SetCurrentState(LogState currentState)
        {
            _logState = currentState;
        }
        public static ParsedLogEntry ParseLine(string logEntry,long lineIndex, bool realTime = true)
        {
            try
            {
                if (_logDate == DateTime.MinValue || realTime)
                    _logDate = DateTime.Now;
                var listEntries = GetInfoComponents(logEntry);

                return _7_0LogParsing.ParseLog(logEntry, lineIndex, _logDate, listEntries, realTime);

            }
            catch (Exception e)
            {
                OnNewLog(e.Message);
                return new ParsedLogEntry() { Error = ErrorType.IncompleteLine };
            }
        }
        public static List<ParsedLogEntry> ParseAllLines(CombatLogFile combatLog)
        {
            CombatLogStateBuilder.ClearState();
            _logDate = combatLog.Time;

            var logLines = new List<string>();
            using (combatLog.Data)
            {
                while (!combatLog.Data.EndOfStream)
                    logLines.Add(combatLog.Data.ReadLine());
            }

            var numberOfLines = logLines.Count;
            ParsedLogEntry[] parsedLog = new ParsedLogEntry[numberOfLines];
            Parallel.For(0, numberOfLines, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                
                if (logLines[i] == "")
                    return;
                var parsedLine = ParseLine(logLines[i], i, false);

                if (parsedLine.Error == ErrorType.IncompleteLine)
                    return;
                parsedLog[i] = parsedLine;
                parsedLog[i].LogName = combatLog.Name;
                
            });
          
            var cleanedLogs = parsedLog.Where(l => l != null);
            CombatTimestampRectifier.RectifyTimeStamps(cleanedLogs.ToList());
            var orderdedLog = cleanedLogs.OrderBy(l => l.TimeStamp);
            UpdateStateAndLogs(orderdedLog.ToList(), false);

            return orderdedLog.ToList();
        }
        private static List<string> GetInfoComponents(string log)
        {
            var returnValues = new List<string>();
            int startIndex = 0;
            int numberOfCloses = 0;
            for (var i = 0; i < log.Length; i++)
            {
                if (log[i] == '[')
                {
                    startIndex = i + 1;
                    continue;
                }
                if (log[i] == ']')
                {
                    returnValues.Add(log.Substring(startIndex, i - startIndex));
                    numberOfCloses++;
                    if (numberOfCloses == 5)
                        return returnValues;
                    else
                        continue;
                }
            }
            return returnValues;
        }
        private static void UpdateStateAndLogs(List<ParsedLogEntry> orderdedLog, bool realTime)
        {
            foreach (var line in orderdedLog)
            {
                SetCurrentState(CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(line, realTime));
            }
        }
    }
}
