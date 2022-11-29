using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;

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
        public static event Action<DateTime> HistoricalLogsFinished = delegate { };
        public static event Action HistoricalLogsStarted = delegate { };
        public event Action<Entity> LocalPlayerIdentified = delegate { };
        public static event Action<ParsedLogEntry> NewLineStreamed = delegate { };

        private bool _isInCombat = false;
        private bool _isWaitingForExitCombatTimout;
        //private long _numberOfProcessedEntries;
        //private long _currentLogsInFile;
        private string _logToMonitor; 
        private bool _monitorLog;
        private long numberOfProcessedBytes = 0;
        private DateTime _lastUpdateTime;
        private List<ParsedLogEntry> _currentFrameData = new List<ParsedLogEntry>();
        private List<ParsedLogEntry> _waitingForExitCombatTimeout = new List<ParsedLogEntry>();
        private List<ParsedLogEntry> _currentCombatData = new List<ParsedLogEntry>();
        private DateTime _currentCombatStartTime;
        private Encoding _fileEncoding;
        public CombatLogStreamer()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _fileEncoding = Encoding.GetEncoding(1252);
            CombatDetector.AlertExitCombatTimedOut += OnExitCombatTimedOut;
        }
        public void MonitorLog(string logToMonitor)
        {
            Logging.LogInfo("Starting live monitor of log - " + logToMonitor);
            Task.Run(() =>
            {
                ResetMonitoring();
                _logToMonitor = logToMonitor;
                ParseExisitingLogs();
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
            var currentLogs = CombatLogParser.ParseAllLines(file,true);
            Logging.LogInfo("Found " + currentLogs.Count + " log entries in "+_logToMonitor);
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
            _currentFrameData.Clear();
        }
        private void ResetMonitoring()
        {
            numberOfProcessedBytes = 0;
            _currentCombatStartTime = DateTime.MinValue;
            _lastUpdateTime = DateTime.MinValue;
        }

        private void PollForUpdates()
        {
            Task.Run(() => {
                while (_monitorLog)
                {
                    GenerateNewFrame();
                    Thread.Sleep(250);
                }
            });
        }
        private void GenerateNewFrame()
        {
            if (!CheckIfStale())
                return;
            ParseLogFile();
        }

        private void ParseLogFile()
        {
            _currentFrameData = new List<ParsedLogEntry>();        
            using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, _fileEncoding))
            {
                List<string> lines = new List<string>();
                bool hasValidEnd = GetNewlines(sr, lines);

                if (lines.Count == 0)
                    return;          
                
                for (var line = 0; line < lines.Count; line++)
                {
                    var result = ProcessNewLine(lines[line], line, Path.GetFileName(_logToMonitor));

                    if (result == ProcessedLineResult.Incomplete)
                    {
                        Logging.LogError("Failed to parse line: " + lines[line]);
                        throw new Exception("Failed to parse line: " + lines[line]);
                    }
                }
                if (!_isInCombat)
                    return;
                CombatTimestampRectifier.RectifyTimeStamps(_currentFrameData);
                var updateMessage = new CombatStatusUpdate { Type = UpdateType.Update, Logs = _currentFrameData, CombatStartTime = _currentCombatStartTime };
                CombatUpdated(updateMessage);
                
            }
        }

        private bool GetNewlines(StreamReader sr, List<string> lines)
        {
            try
            {
                sr.BaseStream.Seek(numberOfProcessedBytes, SeekOrigin.Begin);
                bool hasValidEnd = false;
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
                return false;
            }
            catch(Exception e)
            {
                Logging.LogError("Error occured while parsing log file at position " + numberOfProcessedBytes+" - "+_logToMonitor+"\r\n"+"Exception Message: "+e.Message);
                return false;
            }
        }

        private void ParseHistoricalLog(List<ParsedLogEntry> logs)
        {
            var usableLogs = logs.Where(l => l.Error != ErrorType.IncompleteLine).ToList();
            _currentCombatData.Clear();
            foreach (var t in usableLogs)
            {
                if (t.Source.IsLocalPlayer)
                    LocalPlayerIdentified(t.Source);
                CheckForCombatState(t, false);
                if (_isInCombat)
                {
                    _currentCombatData.Add(t);
                }
            }
            Logging.LogInfo("Parsed existing log - " + _logToMonitor);
            HistoricalLogsFinished(_currentCombatData.Count == 0? DateTime.Now : _currentCombatData.Max(l=>l.TimeStamp));
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

        private ProcessedLineResult ProcessNewLine(string line,long lineIndex,string logName)
        {
            var parsedLine = CombatLogParser.ParseLine(line,lineIndex);
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
                _currentFrameData.Add(parsedLine);
                _currentCombatData.Add(parsedLine);
            }
            if(_isInCombat && _isWaitingForExitCombatTimout)
            {
                _currentFrameData.Add(parsedLine);
                _waitingForExitCombatTimeout.Add(parsedLine);
            }
            return ProcessedLineResult.Success;
        }
        private void CheckForCombatState(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat = true,bool isrealtime = false)
        {
            var currentCombatState = CombatDetector.CheckForCombatState(parsedLine, isrealtime);
            if(currentCombatState == CombatState.ExitedByEntering)
            {
                EndCombat(parsedLine);
                EnterCombat(parsedLine, shouldUpdateOnNewCombat);
            }
            if (currentCombatState == CombatState.EnteredCombat)
            {
                EnterCombat(parsedLine, shouldUpdateOnNewCombat);
            }
            if(currentCombatState == CombatState.ExitedCombat)
            {
                EndCombat(parsedLine);
            }
            if(currentCombatState == CombatState.ExitCombatDetected)
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
            _currentFrameData.Clear();
            _currentCombatData.Clear();
            _isInCombat = true;
            _currentCombatStartTime = parsedLine.TimeStamp;
            _currentCombatData.Add(parsedLine);
            _currentFrameData.Add(parsedLine);
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
            if(_waitingForExitCombatTimeout.Count > 0)
            {
                _currentCombatData.AddRange(_waitingForExitCombatTimeout);
            }
            if (parsedLine != null)
            {
                _currentCombatData.Add(parsedLine);
                _currentFrameData.Add(parsedLine);
            }

            _isInCombat = false;

            if (string.IsNullOrEmpty(_logToMonitor))
                return;
            var updateMessage = new CombatStatusUpdate { Type = UpdateType.Stop, Logs = _currentCombatData, CombatStartTime = _currentCombatStartTime };
            Logging.LogInfo("Sending combat state change notification: " + updateMessage.Type + " at " + updateMessage.CombatStartTime + " with location " + updateMessage.CombatLocation);
            CombatUpdated(updateMessage);
            EncounterTimerTrigger.FireEnded();
        }
    }
}
