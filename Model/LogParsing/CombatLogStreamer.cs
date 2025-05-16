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

        // Instance events
        public event Action<Entity> LocalPlayerIdentified = delegate { };
        public event Action<double> NewLogTimeOffsetMs = delegate { };
        public event Action<double> NewTotalTimeOffsetMs = delegate { };
        public event Action<string> ErrorParsingLogs = delegate { };

        private bool _isInCombat = false;
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

        public CombatLogStreamer()
        {
            _forceUpdateOfLogs = Settings.ReadSettingOfType<bool>("force_log_updates");
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _fileEncoding = Encoding.GetEncoding(1252);
            CombatDetector.AlertExitCombatTimedOut += OnExitCombatTimedOut;
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
            int[] characters = new int[currentLogs.Count];
            Parallel.For(0, currentLogs.Count, i =>
            {
                characters[i] = currentLogs[i].LogBytes;
            });
            _numberOfProcessedBytes = characters.Sum();
            Logging.LogInfo("Processed " + _numberOfProcessedBytes + " bytes of data in " + _logToMonitor);
            ParseHistoricalLog(currentLogs);
        }

        public void StopMonitoring()
        {
            _monitorLog = false;
            EndCombat();
            _currentCombatLogs.Clear();
        }

        private void ResetMonitoring()
        {
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
                if (!CheckIfStale())
                {
                    return;
                }
                ParseLogFile();
            }
        }

private void ParseLogFile()
{
    var logUpdateTime = TimeUtility.CorrectedTime;

    // 1) Remember where we started in the file
    long originalCursor = _numberOfProcessedBytes;

    // 2) Read everything we can—capturing absolute startOffsets but NOT yet
    //    updating _numberOfProcessedBytes.
    List<string> lines       = new();
    List<long>   startOffsets = new();
    using var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    using var sr = new StreamReader(fs, _fileEncoding);
    GetNewlines2(sr, lines, startOffsets);
    // note: GetNewlines2 should only use a temporary local offset var,
    //       *not* write into _numberOfProcessedBytes itself.

    if (lines.Count == 0)
        return;

    // 3) Try to parse every line
    int successfulLines = 0;
    for (int i = 0; i < lines.Count; i++)
    {
        numberOfProcessedLines++;
        var result = ProcessNewLine(lines[i], numberOfProcessedLines, Path.GetFileName(_logToMonitor), logUpdateTime);

        if (result == ProcessedLineResult.Incomplete)
        {
            // rollback line count
            numberOfProcessedLines -= (lines.Count - i);

            // jump the cursor back to the start of this bad line
            _numberOfProcessedBytes = startOffsets[i];

            Logging.LogError($"Incomplete parse on line #{i}, rolling back to byte offset {_numberOfProcessedBytes}");
            break;
        }

        successfulLines++;
    }

    // 4) If every line succeeded, *then* advance the cursor by the total bytes
    //    of all lines we just consumed.
    if (successfulLines == lines.Count)
    {
        long newBytes = lines.Sum(l => _fileEncoding.GetByteCount(l));
        _numberOfProcessedBytes = originalCursor + newBytes;
    }

    // 5) Fire update if needed
    if (_isInCombat)
    {
        var updateMessage = new CombatStatusUpdate {
            Type            = UpdateType.Update,
            Logs            = _currentCombatLogs,
            CombatStartTime = _currentCombatStartTime
        };
        CombatUpdated.InvokeSafely(updateMessage);
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

    long localOffset = _numberOfProcessedBytes;   // snapshot, never write back directly
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
            DateTime combatEndTime = _currentCombatLogs.Count == 0 ? TimeUtility.CorrectedTime : _currentCombatLogs.Max(l => l.TimeStamp);
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

        private ProcessedLineResult ProcessNewLine(string line, long lineIndex, string logName, DateTime logUpdateTime)
        {
            var parsedLine = CombatLogParser.ParseLine(line, lineIndex, _mostRecentLogTime);
            if (parsedLine.Error == ErrorType.IncompleteLine)
            {
                return ProcessedLineResult.Incomplete;
            }
            
            _mostRecentLogTime = parsedLine.TimeStamp;
            var logTimeOffset = Math.Abs((parsedLine.TimeStamp - logUpdateTime).TotalMilliseconds);
            var totalTimeOffset = Math.Abs((parsedLine.TimeStamp - TimeUtility.CorrectedTime).TotalMilliseconds);
            NewLogTimeOffsetMs.InvokeSafely(logTimeOffset);
            NewTotalTimeOffsetMs.InvokeSafely(totalTimeOffset);

            if (parsedLine.Source.IsLocalPlayer)
                LocalPlayerIdentified.InvokeSafely(parsedLine.Source);
            parsedLine.LogName = Path.GetFileName(logName);
            CheckForCombatState(parsedLine, true, true);
            NewLineStreamed.InvokeSafely(parsedLine);
            if (_isInCombat && !_isWaitingForExitCombatTimout)
            {
                _currentCombatLogs.Add(parsedLine);
            }
            if (_isInCombat && _isWaitingForExitCombatTimout)
            {
                _currentCombatLogs.Add(parsedLine);
                _waitingForExitCombatTimeout.Add(parsedLine);
            }
            return ProcessedLineResult.Success;
        }

        private void CheckForCombatState(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat, bool isrealtime)
        {
            var currentCombatState = CombatDetector.CheckForCombatState(parsedLine, isrealtime);
            if (currentCombatState == CombatState.ExitedByEntering)
            {
                EndCombat(parsedLine);
                EnterCombat(parsedLine, shouldUpdateOnNewCombat, isrealtime);
            }
            if (currentCombatState == CombatState.EnteredCombat)
            {
                EnterCombat(parsedLine, shouldUpdateOnNewCombat, isrealtime);
            }
            if (currentCombatState == CombatState.ExitedCombat)
            {
                EndCombat(parsedLine);
            }
            if (currentCombatState == CombatState.ExitCombatDetected)
            {
                _isWaitingForExitCombatTimout = true;
            }
        }

        private void OnExitCombatTimedOut(CombatState state)
        {
            _isWaitingForExitCombatTimout = false;
            _waitingForExitCombatTimeout.Clear();
            EndCombat();
        }

        private void EnterCombat(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat, bool isrealtime)
        {
            Logging.LogInfo("Parsing... Starting combat");
            _currentCombatLogs.Clear();
            _isInCombat = true;
            _currentCombatStartTime = parsedLine.TimeStamp;
            _currentCombatLogs.Add(parsedLine);
            var updateMessage = new CombatStatusUpdate
            {
                Type = UpdateType.Start,
                CombatStartTime = _currentCombatStartTime,
                CombatLocation = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(parsedLine.TimeStamp).Name
            };
            if (shouldUpdateOnNewCombat)
            {
                EncounterTimerTrigger.CurrentEncounter = ("", "", "");
                CombatStarted.InvokeSafely();
                CombatUpdated.InvokeSafely(updateMessage);
            }
        }

        private void EndCombat(ParsedLogEntry parsedLine = null)
        {
            Logging.LogInfo("Parsing... Ending combat");
            
            _isWaitingForExitCombatTimout = false;
            if (!_isInCombat)
                return;
            if (_waitingForExitCombatTimeout.Count > 0)
            {
                _currentCombatLogs.AddRange(_waitingForExitCombatTimeout);
            }
            if (parsedLine != null)
            {
                _currentCombatLogs.Add(parsedLine);
            }

            _isInCombat = false;

            if (string.IsNullOrEmpty(_logToMonitor))
                return;
            var updateMessage = new CombatStatusUpdate { Type = UpdateType.Stop, Logs = _currentCombatLogs, CombatStartTime = _currentCombatStartTime };
            Logging.LogInfo("Sending combat state change notification: " + updateMessage.Type + " at " + updateMessage.CombatStartTime + " with location " + updateMessage.CombatLocation);
            CombatUpdated.InvokeSafely(updateMessage);
        }
    }
}
