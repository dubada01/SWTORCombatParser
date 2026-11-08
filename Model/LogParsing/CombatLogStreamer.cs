using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly object _monitoringLock = new();
        private Task _monitoringTask = Task.CompletedTask;
        private int _monitoringVersion;
        private bool _followNewestLog = true;
        private long _numberOfProcessedBytes = 0;
        private string? _pendingMalformedLine;
        private long _pendingMalformedOffset;
        private long _pendingMalformedSince;
        private static readonly TimeSpan MalformedTailRetryWindow = TimeSpan.FromSeconds(3);
        private List<ParsedLogEntry> _currentCombatLogs = new List<ParsedLogEntry>();
        private readonly List<ParsedLogEntry> _pendingLiveLogs = new List<ParsedLogEntry>();
        private readonly object _combatLock = new object();
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
            StartParsing(logToMonitor, true);
        }

        public Task MonitorReplayAsync(string log, DateTime parseDate, CancellationToken cancellationToken)
        {
            if (!DebugFeatures.LogReplayEnabled) throw new InvalidOperationException("Log replay is available only in Debug builds.");
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            StartParsing(log, true, false, ready, parseDate);
            return ready.Task.WaitAsync(cancellationToken);
        }

        private void StartParsing(string log, bool monitor, bool followNewestLog = true,
            TaskCompletionSource? ready = null, DateTime? parseDate = null)
        {
            lock (_monitoringLock)
            {
                var version = Interlocked.Increment(ref _monitoringVersion);
                _monitoringTask = _monitoringTask.ContinueWith(previous =>
                {
                    _ = previous.Exception;
                    if (version != Volatile.Read(ref _monitoringVersion))
                    {
                        ready?.TrySetCanceled();
                        return;
                    }
                    try
                    {
                        ResetMonitoring();
                        _logToMonitor = log;
                        _followNewestLog = followNewestLog;
                        ParseExisitingLogs(monitor, parseDate);
                        LoadingWindowFactory.HideLoading();
                        ready?.TrySetResult();
                        if (monitor)
                            PollForUpdates(version);
                    }
                    catch (Exception e)
                    {
                        ready?.TrySetException(e);
                        LoadingWindowFactory.HideLoading();
                        Logging.LogError("Error during log monitoring: " + e.Message);
                        _currentCombatLogs.Clear();
                        ErrorParsingLogs.InvokeSafely(JsonConvert.SerializeObject(e));
                    }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            }
        }

        public void ParseCompleteLog(string log)
        {
            Logging.LogInfo("Loading existing log - " + log);
            StartParsing(log, false);
        }

        private void ParseExisitingLogs(bool deferIncompleteTail, DateTime? parseDate = null)
        {
            HistoricalLogsStarted.InvokeSafely();
            var file = CombatLogLoader.LoadSpecificLog(_logToMonitor);
            CombatLogParser.SetParseDate(parseDate ?? file.Time);
            List<ParsedLogEntry> currentLogs;
            using (file.Data)
            {
                currentLogs = CombatLogParser.ParseAllLines(file, true, out _numberOfProcessedBytes, out numberOfProcessedLines, deferIncompleteTail);
                _fileEncoding = file.Data.CurrentEncoding;
            }
            Logging.LogInfo("Found " + currentLogs.Count + " log entries in " + _logToMonitor);
            Logging.LogInfo("Processed " + _numberOfProcessedBytes + " bytes of data in " + _logToMonitor);
            ParseHistoricalLog(currentLogs);
            _mostRecentLogTime = currentLogs.Where(l => l.Error != ErrorType.IncompleteLine).Select(l => l.TimeStamp).DefaultIfEmpty(DateTime.MinValue).Max();
            if (_mostRecentLogTime != DateTime.MinValue)
            {
                _7_0LogParsing.SetStartDate(_mostRecentLogTime);
                EncounterTimerTrigger.SetPvpStateAfterHistorical(_mostRecentLogTime);
            }
        }

        public Task StopMonitoring(bool readFinalFrame = false)
        {
            // Log callbacks can synchronously dispatch to the UI. Never make the
            // UI wait synchronously for the lock held while those callbacks run.
            lock (_monitoringLock)
            {
                Interlocked.Increment(ref _monitoringVersion);
                _monitoringTask = _monitoringTask.ContinueWith(previous =>
                {
                    _ = previous.Exception;
                    lock (_combatLock)
                    {
                        if (readFinalFrame && File.Exists(_logToMonitor)) ParseLogFile();
                        EndCombat(true);
                        _combatDetector.Reset();
                        _currentCombatLogs.Clear();
                    }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
                return _monitoringTask;
            }
        }

        private void ResetMonitoring()
        {
            CombatLogLoader.RefreshSWTORCombatLogsDirectory();
            _numberOfProcessedBytes = 0;
            numberOfProcessedLines = 0;
            _pendingMalformedLine = null;
            _currentCombatStartTime = DateTime.MinValue;
            _lastUpdateTime = DateTime.MinValue;
        }

        private void PollForUpdates(int version)
        {
            while (version == Volatile.Read(ref _monitoringVersion))
            {
                GenerateNewFrame();
                Thread.Sleep(250);
            }
        }

        private void GenerateNewFrame()
        {
            if (!_followNewestLog)
            {
                ParseLogFile();
                return;
            }
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
            lock (_combatLock)
            {
                var logUpdateTime = TimeUtility.CorrectedTime;
                long originalCursor = _numberOfProcessedBytes;
                long consumedBytes = 0;
                var lines = new List<string>();
                using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, _fileEncoding))
                    GetNewlines2(sr, lines);

                for (int i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    var lineBytes = _fileEncoding.GetByteCount(line);
                    var lineOffset = originalCursor + consumedBytes;
                    var isLastLine = i == lines.Count - 1;

                    if (_pendingMalformedLine == line && _pendingMalformedOffset == lineOffset)
                    {
                        // A writer can temporarily terminate a partial record and
                        // replace that terminator when the rest arrives. Wait briefly
                        // for a change, but do not block later records indefinitely.
                        if (isLastLine && Stopwatch.GetElapsedTime(_pendingMalformedSince) < MalformedTailRetryWindow)
                            break;

                        _pendingMalformedLine = null;
                        Logging.LogError($"Skipping malformed log line #{numberOfProcessedLines}");
                        numberOfProcessedLines++;
                        consumedBytes += lineBytes;
                        continue;
                    }

                    _pendingMalformedLine = null;
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        numberOfProcessedLines++;
                        consumedBytes += lineBytes;
                        continue;
                    }

                    var result = ProcessNewLine(line, numberOfProcessedLines, _logToMonitor, logUpdateTime, out var parsed);
                    if (result == ProcessedLineResult.Incomplete)
                    {
                        if (isLastLine)
                        {
                            _pendingMalformedLine = line;
                            _pendingMalformedOffset = lineOffset;
                            _pendingMalformedSince = Stopwatch.GetTimestamp();
                            break;
                        }

                        Logging.LogError($"Skipping malformed log line #{numberOfProcessedLines}");
                    }
                    else if (_isInCombat)
                    {
                        _pendingLiveLogs.Add(parsed);
                    }

                    numberOfProcessedLines++;
                    consumedBytes += lineBytes;
                }

                _numberOfProcessedBytes = originalCursor + consumedBytes;
                FlushPendingLiveLogs();
            }
        }

private void FlushPendingLiveLogs()
{
    if (_pendingLiveLogs.Count > 0)
    {
        var updateMsg = new CombatStatusUpdate
        {
            Type            = UpdateType.Update,
            Logs            = new List<ParsedLogEntry>(_pendingLiveLogs),
            CombatStartTime = _currentCombatStartTime
        };
        _pendingLiveLogs.Clear();
        CombatUpdated.InvokeSafely(updateMsg);
    }
}


        private void GetNewlines2(
            StreamReader sr,
            List<string> lines)
        {
            // 0) Reposition the reader to where we left off last time
            sr.DiscardBufferedData();
            sr.BaseStream.Seek(_numberOfProcessedBytes, SeekOrigin.Begin);

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

                    if (c == '\n')
                    {
                        var complete = newLine.ToString() + (seenCR ? "\r\n" : "\n");
                        lines.Add(complete);

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
            lock (_combatLock)
            {
            Logging.LogInfo("Parsing... Ending combat");
            if (!_isInCombat)
                return;
            if (isRealTime)
                FlushPendingLiveLogs();
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
}
