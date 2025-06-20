using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SWTORCombatParser.Model.LogParsing
{
    public enum ProcessedLineResult
    {
        Success,
        Incomplete,
        Repeat
    }

    public class CombatLogStreamer
    {
        // Static events
        public static event Action CombatStarted = delegate { };
        public static event Action<CombatStatusUpdate> CombatUpdated = delegate { };
        public static event Action<DateTime, bool> HistoricalLogsFinished = delegate { };
        public static event Action HistoricalLogsStarted = delegate { };
        public static event Action<ParsedLogEntry> NewLineStreamed = delegate { };
        public static bool InCombat => _isInCombat;

        // Instance events
        public event Action<Entity> LocalPlayerIdentified = delegate { };
        public event Action<double> NewLogTimeOffsetMs = delegate { };
        public event Action<double> NewTotalTimeOffsetMs = delegate { };
        public event Action<string> ErrorParsingLogs = delegate { };

        private static bool _isInCombat = false;
        private bool _isWaitingForExitCombatTimout;

        private int numberOfProcessedLines = 0;
        private string _logToMonitor;
        private bool _monitorLog;
        private long _numberOfProcessedBytes = 0;
        private List<ParsedLogEntry> _currentCombatLogs = new List<ParsedLogEntry>();
        private List<ParsedLogEntry> _waitingForExitCombatTimeout = new List<ParsedLogEntry>();
        private DateTime _currentCombatStartTime;
        private DateTime _lastUpdateTime;
        private Encoding _fileEncoding;
        private bool _forceUpdateOfLogs = false;
        private DateTime _mostRecentLogTime;
        private DateTime _staleCheckTime;
        private int _staleCheckIntervalSec = 5;
        private CombatDetector _combatDetector;

        public CombatLogStreamer()
        {
            _combatDetector = new CombatDetector();
            _forceUpdateOfLogs = Settings.ReadSettingOfType<bool>("force_log_updates");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _fileEncoding = Encoding.GetEncoding(1252);
            _combatDetector.AlertExitCombatTimedOut += OnExitCombatTimedOut;
            _7_0LogParsing.SetupRegex();
        }

        public string CurrentLog => _logToMonitor;

        public void MonitorLog(string logToMonitor)
        {
            Logging.LogInfo("Starting live monitor of log - " + logToMonitor);
            Task.Run(() =>
            {
                try
                {
                    ResetMonitoring();
                    _logToMonitor = logToMonitor;
                    ParseExisitingLogs();
                    LoadingWindowFactory.HideLoading();
                    _monitorLog = true;
                    PollForUpdates();
                }
                catch (Exception e)
                {
                    LoadingWindowFactory.HideLoading();
                    Logging.LogError("Error during log monitoring: " + e.Message);
                    _monitorLog = false;
                    _currentCombatLogs.Clear();
                    ErrorParsingLogs.InvokeSafely(JsonConvert.SerializeObject(e));
                }
            });
        }

        public void ParseCompleteLog(string log)
        {
            Logging.LogInfo("Loading existing log - " + log);
            ResetMonitoring();
            _logToMonitor = log;
            Task.Run(ParseExisitingLogs);
        }

        private void ParseExisitingLogs()
        {
            HistoricalLogsStarted.InvokeSafely();
            var file = CombatLogLoader.LoadSpecificLog(_logToMonitor);
            CombatLogParser.SetParseDate(file.Time);
            var currentLogs = CombatLogParser.ParseAllLines(file, true);
            numberOfProcessedLines = currentLogs.Count;
            Logging.LogInfo("Found " + currentLogs.Count + " log entries in " + _logToMonitor);
            // Single-pass, no extra array
            long total = 0;
            foreach (var log in currentLogs)
                total += log.LogBytes + 2;
            _numberOfProcessedBytes = total;
            Logging.LogInfo("Processed " + _numberOfProcessedBytes + " bytes of data in " + _logToMonitor);
            ParseHistoricalLog(currentLogs);
            if(currentLogs.Count > 0)
                EncounterTimerTrigger.SetPvpStateAfterHistorical(currentLogs.Last().TimeStamp);
        }

        public void StopMonitoring()
        {
            _monitorLog = false;
            EndCombat(true);
            _currentCombatLogs.Clear();
        }

        private void ResetMonitoring()
        {
            CombatLogLoader.RefreshSWTORCombatLogsDirectory();
            _numberOfProcessedBytes = 0;
            numberOfProcessedLines = 0;
            _currentCombatStartTime = DateTime.MinValue;
            _lastUpdateTime = DateTime.MinValue;
        }

        private void PollForUpdates()
        {
            Task.Run(() =>
            {
                while (_monitorLog)
                {
                    GenerateNewFrame();
                    Thread.Sleep(250);
                }
            });
        }

        private void GenerateNewFrame()
        {
            if (_forceUpdateOfLogs)
            {
                ConfirmUsingMostRecentLog();
                ParseLogFile();
            }
            else
            {
                if (DateTime.Now > _staleCheckTime && !CheckIfStale())
                {
                    return;
                }

                ParseLogFile();
            }
        }

private void ParseLogFile()
{
    var logUpdateTime = TimeUtility.CorrectedTime;
    long originalCursor = _numberOfProcessedBytes;

    // 1) Read all new lines since last cursor
    var lines        = new List<string>();
    var startOffsets = new List<long>();
    using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
    using (var sr = new StreamReader(fs, _fileEncoding))
    {
        GetNewlines2(sr, lines, startOffsets);
    }

    if (lines.Count == 0)
        return;

    // 2) Parse each, collect only those in‐combat this frame
    var newLogs         = new List<ParsedLogEntry>();
    int successfulLines = 0;

    for (int i = 0; i < lines.Count; i++)
    {
        numberOfProcessedLines++;

        var result = ProcessNewLine(
            lines[i],
            numberOfProcessedLines,
            /*logFilePath*/ _logToMonitor,
            logUpdateTime,
            out var parsed);

        if (result == ProcessedLineResult.Incomplete)
        {
            // rollback to the byte offset of the bad line
            numberOfProcessedLines -= (lines.Count - i);
            _numberOfProcessedBytes = startOffsets[i];
            Logging.LogError($"Incomplete parse on line #{i}, rolling back to byte offset {_numberOfProcessedBytes}");
            break;
        }

        successfulLines++;
        if (_isInCombat)
            newLogs.Add(parsed);
    }

    // 3) Advance cursor if everything succeeded
    if (successfulLines == lines.Count)
    {
        long consumedBytes = lines.Sum(l => _fileEncoding.GetByteCount(l));
        _numberOfProcessedBytes = originalCursor + consumedBytes;
    }

    // 4) Fire one Update with only this frame’s logs
    if (_isInCombat && newLogs.Count > 0)
    {
        var updateMsg = new CombatStatusUpdate
        {
            Type            = UpdateType.Update,
            Logs            = newLogs,
            CombatStartTime = _currentCombatStartTime
        };
        CombatUpdated.InvokeSafely(updateMsg);
    }
}


        private void GetNewlines2(
            StreamReader sr,
            List<string> lines,
            List<long> lineStartOffsets)
        {
            // 0) Reposition the reader to where we left off last time
            sr.DiscardBufferedData();
            sr.BaseStream.Seek(_numberOfProcessedBytes, SeekOrigin.Begin);

            long localOffset = _numberOfProcessedBytes; // snapshot, never write back directly
            bool seenCR = false;
            var newLine = new StringBuilder();
            char[] buffer = new char[2500];
            int readCount;

            while ((readCount = sr.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < readCount; i++)
                {
                    char c = buffer[i];
                    if (c == '\0')
                    {
                        seenCR = false;
                        break;
                    }

                    if (c == '\r')
                    {
                        seenCR = true;
                        continue;
                    }

                    if (c == '\n' && seenCR)
                    {
                        // complete line (including CRLF)
                        var complete = newLine.ToString() + "\r\n";
                        lines.Add(complete);
                        lineStartOffsets.Add(localOffset);

                        // advance our local offset by the byte‐count of that line
                        int bc = _fileEncoding.GetByteCount(complete);
                        localOffset += bc;

                        newLine.Clear();
                        seenCR = false;
                        continue;
                    }

                    if (seenCR)
                    {
                        // stray CR, treat it as part of the content
                        newLine.Append('\r');
                        seenCR = false;
                    }

                    newLine.Append(c);
                }
            }

            // any trailing partial line stays in 'newLine' for next pass
        }


        private void ParseHistoricalLog(List<ParsedLogEntry> logs)
        {
            var usableLogs = logs.Where(l => l.Error != ErrorType.IncompleteLine).ToList();
            _currentCombatLogs.Clear();
            var localPlayerIdentified = false;
            foreach (var t in usableLogs)
            {
                if (t.Source.IsLocalPlayer)
                {
                    LocalPlayerIdentified.InvokeSafely(t.Source);
                    localPlayerIdentified = true;
                }

                CheckForCombatState(t, false, false);
                if (_isInCombat)
                {
                    _currentCombatLogs.Add(t);
                }
            }

            Logging.LogInfo("Parsed existing log - " + _logToMonitor);
            DateTime combatEndTime = _currentCombatLogs.Count == 0
                ? TimeUtility.CorrectedTime
                : _currentCombatLogs.Max(l => l.TimeStamp);
            HistoricalLogsFinished.InvokeSafely(combatEndTime, localPlayerIdentified);
        }

        private void ConfirmUsingMostRecentLog()
        {
            var mostRecentFile = CombatLogLoader.GetMostRecentLogPath();
            if (mostRecentFile != _logToMonitor)
            {
                _logToMonitor = mostRecentFile;
                ResetMonitoring();
            }
        }

        private bool CheckIfStale()
        {
            _staleCheckTime = DateTime.Now.AddSeconds(_staleCheckIntervalSec);
            var mostRecentFile = CombatLogLoader.GetMostRecentLogPath();
            if (mostRecentFile != _logToMonitor)
            {
                _logToMonitor = mostRecentFile;
                ResetMonitoring();
                return true;
            }

            var fileInfo = new FileInfo(_logToMonitor);
            if (fileInfo.LastWriteTime == _lastUpdateTime)
                return false;
            _lastUpdateTime = fileInfo.LastWriteTime;
            return true;
        }

        private ProcessedLineResult ProcessNewLine(
            string             line,
            long               lineIndex,
            string             logFilePath,
            DateTime           logUpdateTime,
            out ParsedLogEntry parsedLine)
        {
            // 1) Parse
            parsedLine = CombatLogParser.ParseLine(line, lineIndex, _mostRecentLogTime);

            // 2) If incomplete, bail
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return ProcessedLineResult.Incomplete;

            // 3) Update timestamp tracking
            _mostRecentLogTime = parsedLine.TimeStamp;

            // 4) Fire timing events
            var logTimeOffset   = Math.Abs((parsedLine.TimeStamp - logUpdateTime).TotalMilliseconds);
            var totalTimeOffset = Math.Abs((parsedLine.TimeStamp - TimeUtility.CorrectedTime).TotalMilliseconds);
            NewLogTimeOffsetMs.InvokeSafely(logTimeOffset);
            NewTotalTimeOffsetMs.InvokeSafely(totalTimeOffset);

            // 5) Local‐player detection
            if (parsedLine.Source.IsLocalPlayer)
                LocalPlayerIdentified.InvokeSafely(parsedLine.Source);

            // 6) Record which file this came from
            parsedLine.LogName = Path.GetFileName(logFilePath);

            // 7) Combat entry/exit detection
            CheckForCombatState(parsedLine, /*shouldUpdateOnNewCombat*/ true, /*isRealtime*/ true);

            // 8) Fire the single‐line event
            NewLineStreamed.InvokeSafely(parsedLine);

            // 9) Return success
            return ProcessedLineResult.Success;
        }

        private void CheckForCombatState(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat, bool isrealtime)
        {
            var currentCombatState = _combatDetector.CheckForCombatState(parsedLine, isrealtime);
            if (currentCombatState == CombatState.ExitedByEntering)
            {
                EndCombat(isrealtime,parsedLine);
                EnterCombat(parsedLine, shouldUpdateOnNewCombat, isrealtime);
            }

            if (currentCombatState == CombatState.EnteredCombat)
            {
                EnterCombat(parsedLine, shouldUpdateOnNewCombat, isrealtime);
            }

            if (currentCombatState == CombatState.ExitedCombat)
            {
                EndCombat(isrealtime, parsedLine);
            }
        }

        private void OnExitCombatTimedOut(CombatState state, bool realTime)
        {
            EndCombat(realTime);
        }

        private void EnterCombat(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat, bool isrealtime)
        {
            Logging.LogInfo("Parsing... Starting combat");
            _currentCombatLogs.Clear();
            _isInCombat = true;
            _currentCombatStartTime = parsedLine.TimeStamp;
            _currentCombatLogs.Add(parsedLine);

            var startMsg = new CombatStatusUpdate
            {
                Type = UpdateType.Start,
                CombatStartTime = _currentCombatStartTime,
                CombatLocation = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(parsedLine.TimeStamp).Name
            };

            if (shouldUpdateOnNewCombat)
            {
                EncounterTimerTrigger.CurrentEncounter = ("", "", "");
                CombatStarted.InvokeSafely();
                CombatUpdated.InvokeSafely(startMsg);
            }
        }

        private void EndCombat(bool isRealTime, ParsedLogEntry parsedLine = null)
        {
            Logging.LogInfo("Parsing... Ending combat");
            if (!_isInCombat)
                return;
            var logsToSend = isRealTime ? new List<ParsedLogEntry>() : _currentCombatLogs;
            if (parsedLine != null)
                logsToSend.Add(parsedLine);

            _isInCombat = false;

            if (string.IsNullOrEmpty(_logToMonitor))
                return;

            // On Stop we still send the *full* combat for any teardown needs
            var stopMsg = new CombatStatusUpdate
            {
                Type = UpdateType.Stop,
                Logs = logsToSend,
                CombatStartTime = _currentCombatStartTime
            };
            Logging.LogInfo($"Sending combat state change notification: {stopMsg.Type} at {stopMsg.CombatStartTime} with location {stopMsg.CombatLocation}");
            CombatUpdated.InvokeSafely(stopMsg);
        }

    }
}
