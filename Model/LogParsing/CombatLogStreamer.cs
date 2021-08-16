using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CloudRaiding;
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
        public event Action<List<ParsedLogEntry>> CombatStopped = delegate { };
        public CombatLogStreamer()
        {

        }
        private bool _isInCombat = false;
        private bool _combatEnding = false;
        private long _numberOfEntries;
        private long _newNumberOfEntries;
        private string _logToMonitor; 
        private long _combatEndLineIndex;
        private bool _monitorLog;
        private DateTime _lastUpdateTime;
        private List<ParsedLogEntry> _currentFrameData = new List<ParsedLogEntry>();
        private List<ParsedLogEntry> _currentCombatData = new List<ParsedLogEntry>();
        public void MonitorLog(string logToMonitor)
        {
            _newNumberOfEntries = 0;
            _numberOfEntries = 0;
            _combatEndLineIndex = 0;
            _lastUpdateTime = DateTime.MinValue;
            _logToMonitor = logToMonitor;
            _monitorLog = true;
            _firstTimeThroughLog = true;
            PollForUpdates();
        }
        public void StopMonitoring()
        {
            _monitorLog = false;
            EndCombat();
            _currentFrameData.Clear();
        }

        private void PollForUpdates()
        {
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
            ParseLogFile(_logToMonitor);
        }
        private bool _firstTimeThroughLog = true;
        internal void ParseLogFile(string logToMonitor)
        {
            _currentFrameData = new List<ParsedLogEntry>();
            using (var fs = new FileStream(logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.Default))
            {
                var currentState = sr.ReadToEnd().Split('\n');
                _newNumberOfEntries = currentState.Length;
                if (_newNumberOfEntries - 1 == _numberOfEntries)
                    return;
                CombatLogParser.BuildLogState(CombatLogLoader.LoadSpecificLog(logToMonitor));
                for (var line = _numberOfEntries; line < currentState.Length; line++)
                {
                    ProcessNewLine(currentState[line], line, logToMonitor);
                }
                _numberOfEntries = _newNumberOfEntries - 1;
                if (!_isInCombat)
                    return;
                _firstTimeThroughLog = false;
                if (CombatLogParser.CurrentRaidGroup != null)
                {
                    var cloudRaiding = new PostgresConnection();
                    Parallel.ForEach(_currentFrameData, log =>
                    {
                        cloudRaiding.AddLog(CombatLogParser.CurrentRaidGroup.GroupId, log);
                    });
                }
                else
                {
                    NewLogEntries(_currentFrameData);
                }
            }
        }

        private bool CheckIfStale()
        {
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
                CheckForCombatEnd(lineIndex);
                return;
            }
            var parsedLine = CombatLogParser.ParseLine(line);
            parsedLine.LogName = Path.GetFileName(logName);
            if (parsedLine.Error == ErrorType.IncompleteLine)
            {
                return;
            }
            CheckForCombatState(lineIndex, parsedLine);
        }

        private void CheckForCombatState(long lineIndex, ParsedLogEntry parsedLine)
        {
            if (parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "EnterCombat")
            {
                _isInCombat = true; 
                if (CombatLogParser.CurrentRaidGroup == null)
                    CombatStarted(parsedLine.Source.Name, parsedLine.Value.StrValue);
            }
            if (parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "ExitCombat")
            {
                _combatEnding = true;
                _combatEndLineIndex = lineIndex;
            }
            if ((parsedLine.Effect.EffectName == "Death" && parsedLine.Target.IsPlayer))
                EndCombat();
            CheckForCombatEnd(lineIndex);
            if (_isInCombat)
            {
                _currentFrameData.Add(parsedLine);
                _currentCombatData.Add(parsedLine);
            }
        }

        private void CheckForCombatEnd(long lineIndex)
        {
            if ((lineIndex - _combatEndLineIndex > 10 || (lineIndex == _newNumberOfEntries - 1)) && _combatEnding)
            {
                EndCombat();
            }
        }
        private void EndCombat()
        {
            _isInCombat = false;
            _combatEnding = false;
            if (CombatLogParser.CurrentRaidGroup != null && !_firstTimeThroughLog)
            {
                var cloudRaiding = new PostgresConnection();
                Parallel.ForEach(_currentFrameData, log =>
                {
                    cloudRaiding.AddLog(CombatLogParser.CurrentRaidGroup.GroupId, log);
                });
            }
            else
            {
                if(CombatLogParser.CurrentRaidGroup == null)
                    CombatStopped(_currentCombatData);
            }
            
            _currentCombatData.Clear();
        }
    }
}
