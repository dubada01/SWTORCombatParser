using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        public static event Action<CombatStatusUpdate> CombatUpdated = delegate { };
        public static event Action<DateTime, bool> HistoricalLogsFinished = delegate { };
        public static event Action HistoricalLogsStarted = delegate { };
        public event Action<Entity> LocalPlayerIdentified = delegate { };
        public event Action<double> NewLogTimeOffsetMs = delegate { };
        public event Action<double> NewTotalTimeOffsetMs = delegate { };
        public static event Action<ParsedLogEntry> NewLineStreamed = delegate { };

        private bool _isInCombat = false;
        private bool _isWaitingForExitCombatTimout;

        private string _logToMonitor;
        private bool _monitorLog;
        private long numberOfProcessedBytes = 0;
        private List<ParsedLogEntry> _currentCombatLogs = new List<ParsedLogEntry>();
        private List<ParsedLogEntry> _waitingForExitCombatTimeout = new List<ParsedLogEntry>();
        private DateTime _currentCombatStartTime;
        private DateTime _lastUpdateTime;
        private Encoding _fileEncoding;
        private bool _forceUpdateOfLogs = false;
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
                ResetMonitoring();
                _logToMonitor = logToMonitor;
                ParseExisitingLogs();
                LoadingWindowFactory.HideLoading();
                _monitorLog = true;
                PollForUpdates();
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
            HistoricalLogsStarted();
            var file = CombatLogLoader.LoadSpecificLog(_logToMonitor);
            CombatLogParser.SetParseDate();
            var currentLogs = CombatLogParser.ParseAllLines(file, true);
            Logging.LogInfo("Found " + currentLogs.Count + " log entries in " + _logToMonitor);
            int[] characters = new int[currentLogs.Count];
            Parallel.For(0, currentLogs.Count, i =>
            {
                characters[i] = _fileEncoding.GetByteCount(currentLogs[i].LogText);
            });
            numberOfProcessedBytes = characters.Sum();
            Logging.LogInfo("Processed " + numberOfProcessedBytes + " bytes of data in " + _logToMonitor);
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
            numberOfProcessedBytes = 0;
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
            using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, _fileEncoding))
            {
                List<string> lines = new List<string>();
                GetNewlines(sr, lines);
                sr.Close();
                fs.Close();
                if (lines.Count == 0)
                    return;

                for (var line = 0; line < lines.Count; line++)
                {
                    var result = ProcessNewLine(lines[line], line, Path.GetFileName(_logToMonitor), logUpdateTime);
                    if (result == ProcessedLineResult.Incomplete)
                    {
                        Logging.LogInfo("Failed to parse line: " + lines[line]);
                        ParseExisitingLogs();
                    }
                }
                if (!_isInCombat)
                    return;
                var updateMessage = new CombatStatusUpdate { Type = UpdateType.Update, Logs = _currentCombatLogs, CombatStartTime = _currentCombatStartTime };
                CombatUpdated(updateMessage);

            }
        }

        private void GetNewlines(StreamReader sr, List<string> lines)
        {
            try
            {
                sr.BaseStream.Seek(numberOfProcessedBytes, SeekOrigin.Begin);
                bool lastValueWasbsR = false;
                StringBuilder newLine = new StringBuilder();

                while (!sr.EndOfStream)
                {
                    char[] readChars = new char[2500];
                    sr.Read(readChars, 0, 2500);
                    for (var c = 0; c < readChars.Length; c++)
                    {
                        if (readChars[c] == '\0')
                        {
                            lastValueWasbsR = false;
                            break;
                        }
                        if (readChars[c] == '\r')
                        {
                            lastValueWasbsR = true;
                            continue;
                        }
                        if (readChars[c] == '\n' && lastValueWasbsR)
                        {
                            lastValueWasbsR = false;
                            if (readChars[2499] == '\0' || sr.EndOfStream)
                            {
                                if (c == readChars.Length - 1 || readChars[c + 1] == '\0')
                                {
                                    lines.Add(newLine + Environment.NewLine);
                                    numberOfProcessedBytes += _fileEncoding.GetByteCount(newLine + Environment.NewLine);
                                    break;
                                }
                                else
                                {
                                    if (newLine.Length == 0)
                                        continue;
                                    numberOfProcessedBytes += _fileEncoding.GetByteCount(newLine + Environment.NewLine);
                                    lines.Add(newLine + Environment.NewLine);
                                    newLine.Clear();
                                }
                            }
                            if (newLine.Length == 0)
                                continue;
                            numberOfProcessedBytes += _fileEncoding.GetByteCount(newLine + Environment.NewLine);
                            lines.Add(newLine + Environment.NewLine);
                            newLine.Clear();

                        }
                        else
                        {
                            newLine.Append(readChars[c]);
                            lastValueWasbsR = false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.LogError("Error occured while parsing log file at position " + numberOfProcessedBytes + " - " + _logToMonitor + "\r\n" + "Exception Message: " + e.Message);
            }
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
                    LocalPlayerIdentified(t.Source);
                    localPlayerIdentified = true;
                }
                CheckForCombatState(t, false);
                if (_isInCombat)
                {
                    _currentCombatLogs.Add(t);
                }
            }
            Logging.LogInfo("Parsed existing log - " + _logToMonitor);
            HistoricalLogsFinished(_currentCombatLogs.Count == 0 ? TimeUtility.CorrectedTime : _currentCombatLogs.Max(l => l.TimeStamp), localPlayerIdentified);
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
        private DateTime _mostRecentLogTime;

        private ProcessedLineResult ProcessNewLine(string line, long lineIndex, string logName, DateTime logUpdateTime)
        {
            var parsedLine = CombatLogParser.ParseLine(line, lineIndex, _mostRecentLogTime);
            _mostRecentLogTime = parsedLine.TimeStamp;
            var logTimeOffset = Math.Abs((parsedLine.TimeStamp - logUpdateTime).TotalMilliseconds);
            var totalTimeOffset = Math.Abs((parsedLine.TimeStamp - TimeUtility.CorrectedTime).TotalMilliseconds);
            NewLogTimeOffsetMs(logTimeOffset);
            NewTotalTimeOffsetMs(totalTimeOffset);
            if (parsedLine.Error == ErrorType.IncompleteLine)
            {
                return ProcessedLineResult.Incomplete;
            }

            if (parsedLine.Source.IsLocalPlayer)
                LocalPlayerIdentified(parsedLine.Source);
            parsedLine.LogName = Path.GetFileName(logName);
            CheckForCombatState(parsedLine);
            NewLineStreamed(parsedLine);
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
        private void CheckForCombatState(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat = true, bool isrealtime = false)
        {
            var currentCombatState = CombatDetector.CheckForCombatState(parsedLine, isrealtime);
            if (currentCombatState == CombatState.ExitedByEntering)
            {
                EndCombat(parsedLine);
                EnterCombat(parsedLine, shouldUpdateOnNewCombat);
            }
            if (currentCombatState == CombatState.EnteredCombat)
            {
                EnterCombat(parsedLine, shouldUpdateOnNewCombat);
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
        private void EnterCombat(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat)
        {
            Logging.LogInfo("Parsing... Starting combat");
            _currentCombatLogs.Clear();
            _isInCombat = true;
            _currentCombatStartTime = parsedLine.TimeStamp;
            _currentCombatLogs.Add(parsedLine);
            var updateMessage = new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = _currentCombatStartTime, CombatLocation = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(parsedLine.TimeStamp).Name };
            if (shouldUpdateOnNewCombat)
                CombatUpdated(updateMessage);
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
            CombatUpdated(updateMessage);
        }
    }
}
