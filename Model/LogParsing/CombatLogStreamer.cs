using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.Alerts;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.resources;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser
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
        public event Action<string> NewSoftwareLog = delegate { };
        public static event Action HistoricalLogsFinished = delegate { };
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
            ResetMonitoring();
            _logToMonitor = log;
            Task.Run(() =>
            {
                ParseExisitingLogs();
            });
        }

        private void ParseExisitingLogs()
        {
            var file = CombatLogLoader.LoadSpecificLog(_logToMonitor);
            var currentLogs = CombatLogParser.ParseAllLines(file);
            int[] characters = new int[currentLogs.Count];
            Parallel.For(0, currentLogs.Count, i =>
            {
                characters[i] = _fileEncoding.GetByteCount(currentLogs[i].LogText+"\r\n");
            });
            numberOfProcessedBytes = characters.Sum();
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
        internal void ParseLogFile()
        {
            _currentFrameData = new List<ParsedLogEntry>();        
            using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, _fileEncoding))
            {
                ParsingPerformanceProfiler test = new ParsingPerformanceProfiler();
                test.StartLogProcessing();
                List<string> lines = new List<string>();
                bool hasValidEnd = GetNewlines(sr, lines);

                if (lines.Count == 0)
                    return;          
                
                for (var line = 0; line < lines.Count; line++)
                {
                    var result = ProcessNewLine(lines[line], line, Path.GetFileName(_logToMonitor));

                    if (result == ProcessedLineResult.Incomplete)
                    {
                        throw new Exception("Failed to parse line: " + lines[line]);
                    }
                }
                if (!_isInCombat)
                    return;
                CombatTimestampRectifier.RectifyTimeStamps(_currentFrameData);
                var updateMessage = new CombatStatusUpdate { Type = UpdateType.Update, Logs = _currentFrameData, CombatStartTime = _currentCombatStartTime };
                CombatUpdated(updateMessage);
                test.SaveProcessingInfo(lines.Count,true,true);
            }
        }

        private bool GetNewlines(StreamReader sr, List<string> lines)
        {
            sr.BaseStream.Seek(numberOfProcessedBytes, SeekOrigin.Begin);
            bool hasValidEnd = false;
            StringBuilder newLine = new StringBuilder();
            while (!sr.EndOfStream)
            {
                char[] readChars = new char[2500];
                sr.Read(readChars, 0, 2500);

                for (var c = 0; c < readChars.Length; c++)
                {
                    if (readChars[c] == '\0')
                        break;
                    if (readChars[c] == '\r')
                        continue;
                    if (readChars[c] != '\n')
                    {
                        newLine.Append(readChars[c]);
                    }
                    else
                    {
                        if(readChars[2499] == '\0' || sr.EndOfStream)
                        {
                            if (c == readChars.Length - 1 || readChars[c + 1] == '\0')
                            {
                                lines.Add(newLine.ToString() + Environment.NewLine);
                                numberOfProcessedBytes += _fileEncoding.GetByteCount(newLine.ToString() + Environment.NewLine);
                                break;
                            }
                            else
                            {
                                if (newLine.Length == 0)
                                    continue;
                                numberOfProcessedBytes += _fileEncoding.GetByteCount(newLine.ToString() + Environment.NewLine);
                                lines.Add(newLine.ToString() + Environment.NewLine);
                                newLine.Clear();
                            }
                        }
                        if (newLine.Length == 0)
                            continue;
                        numberOfProcessedBytes += _fileEncoding.GetByteCount(newLine.ToString() + Environment.NewLine);
                        lines.Add(newLine.ToString() + Environment.NewLine);
                        newLine.Clear();
                    }
                }
            }

            return hasValidEnd;
        }

        private void ParseHistoricalLog(List<ParsedLogEntry> logs)
        {
            _currentCombatData.Clear();
            for (var l = 0; l < logs.Count;l++)
            {
                if (logs[l].Source.IsLocalPlayer)
                    LocalPlayerIdentified(logs[l].Source);
                CheckForCombatState(logs[l], false);
                if (_isInCombat)
                {
                    _currentCombatData.Add(logs[l]);
                }
            }
            HistoricalLogsFinished();
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
        private void CheckForCombatState(ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat = true)
        {
            var currentCombatState = CombatDetector.CheckForCombatState(parsedLine);
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
            CombatUpdated(updateMessage);
            EncounterTimerTrigger.FireEnded();
        }
    }
}
