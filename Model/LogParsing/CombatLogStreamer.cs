using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.Alerts;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.resources;
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
        public event Action<CombatStatusUpdate> CombatUpdated = delegate { };
        public event Action<string> NewSoftwareLog = delegate { };
        public event Action HistoricalLogsFinished = delegate { };
        public event Action<Entity> LocalPlayerIdentified = delegate { };

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
        private DateTime _currentCombatStartTime;

        public void MonitorLog(string logToMonitor)
        {
            Task.Run(() =>
            {
                ResetMonitoring();
                _logToMonitor = logToMonitor;
                var currentLogs = CombatLogParser.ParseAllLines(CombatLogLoader.LoadSpecificLog(_logToMonitor));
                _numberOfEntries = currentLogs.Count;
                ParseHistoricalLog(currentLogs);
                _monitorLog = true;
                PollForUpdates();
            });
        }
        public void ParseCompleteLog(string log)
        {
            ResetMonitoring();
            _logToMonitor = log;
            Task.Run(() => {
                var currentLogs = CombatLogParser.ParseAllLines(CombatLogLoader.LoadSpecificLog(_logToMonitor));
                _numberOfEntries = currentLogs.Count;
                ParseHistoricalLog(currentLogs);
            });
        }
        public void StopMonitoring()
        {
            _monitorLog = false;
            EndCombat();
            _currentFrameData.Clear();
        }
        private void ResetMonitoring()
        {
            _newNumberOfEntries = 0;
            _numberOfEntries = 0;
            _combatEndTime = DateTime.MinValue;
            _currentCombatStartTime = DateTime.MinValue;
            _lastUpdateTime = DateTime.MinValue;
            _monitorLog = false;
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
            using (var sr = new StreamReader(fs, Encoding.UTF7))
            {
                var allLogEntries = sr.ReadToEnd().Split('\n');
                _newNumberOfEntries = allLogEntries.Length;
                if (_newNumberOfEntries - 1 <= _numberOfEntries)
                    return;

                for (var line = _numberOfEntries; line < allLogEntries.Length; line++)
                {
                    ProcessNewLine(allLogEntries[line], line, Path.GetFileName(_logToMonitor));
                }

                _numberOfEntries = _newNumberOfEntries - 1;
                if (!_isInCombat)
                    return; 
                CombatTimestampRectifier.RectifyTimeStamps(_currentFrameData);
                var updateMessage = new CombatStatusUpdate { Type = UpdateType.Update, Logs = _currentFrameData, CombatStartTime = _currentCombatStartTime };
                CombatUpdated(updateMessage);
            }
        }
        private void ParseHistoricalLog(List<ParsedLogEntry> logs)
        {
            _currentCombatData.Clear();
            for (var l = 0; l < logs.Count;l++)
            {
                if (logs[l].Source.IsLocalPlayer)
                    LocalPlayerIdentified(logs[l].Source);
                CheckForCombatState(l, logs[l], false);
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

            if (parsedLine.Error == ErrorType.IncompleteLine)
            {
                return;
            }
            if (parsedLine.Source.IsLocalPlayer)
                LocalPlayerIdentified(parsedLine.Source);
            parsedLine.LogName = Path.GetFileName(logName);
            CheckForCombatState(lineIndex, parsedLine);
            if (_isInCombat)
            {
                _currentFrameData.Add(parsedLine);
                _currentCombatData.Add(parsedLine);
            }
        }
        
        private void CheckForCombatState(long lineIndex, ParsedLogEntry parsedLine, bool shouldUpdateOnNewCombat = true)
        {
            if (parsedLine.Effect.EffectType == EffectType.Event && (parsedLine.Effect.EffectName == "EnterCombat"))
            {
                if (_combatEnding)
                    EndCombat();
                _combatEnding = false;
                _isInCombat = true;
                _currentCombatStartTime = parsedLine.TimeStamp;
                _currentCombatData.Add(parsedLine);
                var updateMessage = new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = _currentCombatStartTime, CombatLocation = parsedLine.LogLocation };
                if(shouldUpdateOnNewCombat)
                    CombatUpdated(updateMessage);
            }
            if ((parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "ExitCombat") || (parsedLine.Effect.EffectName == "Death" && parsedLine.Target.IsLocalPlayer))
            {
                _combatEnding = true;
                _combatEndTime = parsedLine.TimeStamp;
            }
            CheckForCombatEnd(lineIndex, parsedLine.TimeStamp);
        }

        private void CheckForCombatEnd(long lineIndex,DateTime currentLogTime)
        {
            if (((currentLogTime - _combatEndTime).TotalSeconds > 2 || (lineIndex == _newNumberOfEntries - 1)) && _combatEnding)
            {
                EndCombat();
            }
        }
        private void EndCombat()
        {
            CombatTimestampRectifier.RectifyTimeStamps(_currentFrameData);
            CombatTimestampRectifier.RectifyTimeStamps(_currentCombatData);
            _isInCombat = false;
            _combatEnding = false;

            if (string.IsNullOrEmpty(_logToMonitor))
                return;
            var updateMessage = new CombatStatusUpdate { Type = UpdateType.Stop, Logs = _currentCombatData, CombatStartTime = _currentCombatStartTime };
            CombatUpdated(updateMessage);

            _currentFrameData.Clear();
            _currentCombatData.Clear();
        }
    }
}
