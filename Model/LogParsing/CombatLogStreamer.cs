using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public class CombatLogStreamer
    {
        public event Action<List<ParsedLogEntry>> NewLogEntries = delegate { };
        public event Action<string,string> CombatStarted = delegate { };
        public event Action<string> NewSoftwareLog = delegate { };
        public event Action<List<ParsedLogEntry>> CombatStopped = delegate { };

        private bool _isInCombat = false;
        private bool _combatEnding = false;
        private long _numberOfEntries;
        private long _newNumberOfEntries;
        private string _logToMonitor; 
        private DateTime _combatEndTime;
        private bool _monitorLog;
        private DateTime _lastUpdateTime;
        private List<ParsedLogEntry> _currentFrameData = new List<ParsedLogEntry>();
        private List<ParsedLogEntry> _currentCombatData = new List<ParsedLogEntry>();
        public void MonitorLog(string logToMonitor, bool forRaiding = false, bool isCompleteLog = false)
        {
            _newNumberOfEntries = 0;
            _numberOfEntries = 0;
            _combatEndTime = DateTime.MinValue;
            _lastUpdateTime = DateTime.MinValue;
            _logToMonitor = logToMonitor;
            _monitorLog = true;
            _firstTimeThroughLog = true;
            if (!forRaiding && !isCompleteLog)
                PollForUpdates();
            else
            {
                if(forRaiding)
                    UpdateLogsForRaiding();
                if (isCompleteLog)
                    ParseCompleteLog();
            }
        }
        
        public void StopMonitoring()
        {
            _monitorLog = false;
            EndCombat();
            _currentFrameData.Clear();
        }
        private void UpdateLogsForRaiding()
        {
            Task.Run(() => {
                while (_monitorLog)
                {
                    GenerateNewFrame();
                    Thread.Sleep(1000);
                }
            });
        }
        private void ParseCompleteLog()
        {
            Task.Run(() => {
                CombatLogParser.InitalizeStateFromLog(CombatLogLoader.LoadSpecificLog(_logToMonitor));
                ParseLogFile();
            });
        }
        private void PollForUpdates()
        {
            CombatLogParser.InitalizeStateFromLog(CombatLogLoader.LoadSpecificLog(_logToMonitor));
            Task.Run(() => {
                while (_monitorLog)
                {
                    GenerateNewFrame();
                    Thread.Sleep(500);
                }
            });
        }
        private void GenerateNewFrame()
        {
            if (!CheckIfStale())
                return;
            ParseLogFile();
        }
        private bool _firstTimeThroughLog = true;
        internal void ParseLogFile()
        {
            _currentFrameData = new List<ParsedLogEntry>();
            using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF7))
            {
                var allLogEntries = sr.ReadToEnd().Split('\n');
                _newNumberOfEntries = allLogEntries.Length;
                if (_newNumberOfEntries - 1 == _numberOfEntries)
                    return;
                
                for (var line = _numberOfEntries; line < allLogEntries.Length; line++)
                {
                    ProcessNewLine(allLogEntries[line], line, _logToMonitor);
                }
                _numberOfEntries = _newNumberOfEntries - 1;
                if (!_isInCombat)
                    return;
                _firstTimeThroughLog = false;
                CombatTimestampRectifier.RectifyTimeStamps(_currentFrameData);

                if (CombatLogParser.CurrentRaidGroup != null)
                {
                    try
                    {
                        var cloudRaiding = new PostgresConnection();
                        UploadFrameDataToCloudRaiding(cloudRaiding);
                    }
                    catch (Exception e)
                    {
                        NewSoftwareLog(e.Message);
                    }
                }
                else
                {
                    CombatLogStateBuilder.UpdateCurrentLogState(ref _currentFrameData, _logToMonitor);
                    NewLogEntries(_currentFrameData);
                }
            }
        }

        private bool CheckIfStale()
        {
            var mostRecentFile = CombatLogLoader.GetMostRecentLogPath();
            if(mostRecentFile != _logToMonitor)
            {
                _logToMonitor = mostRecentFile;
                return true;
            }
            var fileInfo = new FileInfo(_logToMonitor);
            if (fileInfo.LastWriteTime == _lastUpdateTime)
                return false;
            _lastUpdateTime = fileInfo.LastWriteTime;
            return true;
        }
        private void ProcessNewLine(string line,long lineIndex,string logName)
        {
            if (string.IsNullOrEmpty(line))
            {
                CheckForCombatEnd(lineIndex,DateTime.MinValue);
                return;
            }
            var parsedLine = CombatLogParser.ParseLine(line,lineIndex);
            parsedLine.LogName = Path.GetFileName(logName);
            if (parsedLine.Error == ErrorType.IncompleteLine)
            {
                return;
            }
            CheckForCombatState(lineIndex, parsedLine);
            if (_isInCombat)
            {
                _currentFrameData.Add(parsedLine);
                _currentCombatData.Add(parsedLine);
            }
        }

        private void CheckForCombatState(long lineIndex, ParsedLogEntry parsedLine)
        {
            if (parsedLine.Effect.EffectType == EffectType.Event && (parsedLine.Effect.EffectName == "EnterCombat"))
            {
                _combatEnding = false;
                _isInCombat = true; 
                if (CombatLogParser.CurrentRaidGroup == null)
                    CombatStarted(parsedLine.Source.Name, parsedLine.Value.StrValue);
            }
            if (parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "ExitCombat")
            {
                _combatEnding = true;
                _combatEndTime = parsedLine.TimeStamp;
            }
            if ((parsedLine.Effect.EffectName == "Death" && parsedLine.Target.IsPlayer))
            {
                _currentFrameData.Add(parsedLine);
                _currentCombatData.Add(parsedLine);
                EndCombat();
            }
            CheckForCombatEnd(lineIndex,parsedLine.TimeStamp);
        }

        private void CheckForCombatEnd(long lineIndex,DateTime currentLogTime)
        {
            if (((currentLogTime - _combatEndTime).TotalSeconds > 2 || (lineIndex == _newNumberOfEntries - 1)) && _combatEnding)
            {
                EndCombat();
            }
        }
        private void UploadFrameDataToCloudRaiding(PostgresConnection cloudRaiding)
        {
            //Parallel.ForEach(_currentFrameData, log =>
            foreach (var log in _currentFrameData)
            {
                if (log.Source.IsCompanion)
                {
                    cloudRaiding.AddLog(CombatLogParser.CurrentRaidGroup.GroupId, log.GetCompanionLog());
                }
                else
                {
                    cloudRaiding.AddLog(CombatLogParser.CurrentRaidGroup.GroupId, log);
                }
                //});
            }
            _currentFrameData.Clear();
        }
        private void EndCombat()
        {
            CombatTimestampRectifier.RectifyTimeStamps(_currentFrameData);
            CombatTimestampRectifier.RectifyTimeStamps(_currentCombatData);
            _isInCombat = false;
            _combatEnding = false;
            if (CombatLogParser.CurrentRaidGroup != null && !_firstTimeThroughLog)
            {
                try
                {
                    UploadLogsToCloud();
                }
                catch(Exception e)
                {
                    NewSoftwareLog(e.Message);
                }
            }
            else
            {
                if (CombatLogParser.CurrentRaidGroup == null)
                    CombatStopped(_currentCombatData);
            }
            _currentFrameData.Clear();
            _currentCombatData.Clear();
        }

        private void UploadLogsToCloud()
        {
            var cloudRaiding = new PostgresConnection();
            UploadFrameDataToCloudRaiding(cloudRaiding);
            cloudRaiding.AddLog(CombatLogParser.CurrentRaidGroup.GroupId,
                ParsedLogEntry.GetEndCombatLog(_currentCombatData.Last().TimeStamp.AddMilliseconds(1), Path.GetFileName(_logToMonitor)));
        }
    }
}
