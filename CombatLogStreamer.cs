using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public class CombatLogStreamer
    {
        public event Action<List<ParsedLogEntry>> NewLogEntries = delegate { };
        public event Action<string> CombatStarted = delegate { };
        public event Action<List<ParsedLogEntry>> CombatStopped = delegate { };
        private bool _isInCombat = false;
        private bool _combatEnding = false;
        private long _numberOfEntries;
        private long _newNumberOfEntries;
        private string _logToMonitor;
        private List<ParsedLogEntry> _currentFrameData;
        private List<ParsedLogEntry> _currentCombatData = new List<ParsedLogEntry>();
        public void MonitorLog(string logToMonitor)
        {
            _logToMonitor = logToMonitor;
            PollForUpdates();
        }
        private void PollForUpdates()
        {
            Task.Run(() => {
                while (true)
                {
                    GenerateNewFrame();
                    Thread.Sleep(250);
                }
            });
        }
        private void GenerateNewFrame()
        {
            _currentFrameData = new List<ParsedLogEntry>();
            using (var fs = new FileStream(_logToMonitor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.Default))
            {
                var currentState = sr.ReadToEnd().Split('\n');
                _newNumberOfEntries = currentState.Length;
                if (_newNumberOfEntries-1 == _numberOfEntries)
                    return;
                for (var line = _numberOfEntries;line < currentState.Length;line++)
                {
                    ProcessNewLine(currentState[line],line);
                }
                _numberOfEntries = _newNumberOfEntries - 1;
                if (!_isInCombat)
                    return;
                Trace.WriteLine("Current Number Of Rows: " + _numberOfEntries);
                NewLogEntries(_currentFrameData);
            }
        }
        private long _linesAtCombatEnd;
        private void ProcessNewLine(string line,long lineIndex)
        {
            if (string.IsNullOrEmpty(line))
            {
                CheckForCombatEnd(lineIndex);
                return;
            }
            var parsedLine = CombatLogParser.ParseLine(line);
            //Trace.WriteLine(line);
            if (parsedLine.Error == ErrorType.IncompleteLine)
            {
                Trace.WriteLine("Received an incomplete line");
                return;
            }
            if (parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "EnterCombat")
            {
                _isInCombat = true;
                CombatStarted(parsedLine.Source.Name);
            }
            if (parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "ExitCombat")
            {
                _combatEnding = true;
                _linesAtCombatEnd = lineIndex;
            }
            CheckForCombatEnd(lineIndex);
            if (_isInCombat)
            { 
                _currentFrameData.Add(parsedLine);
                _currentCombatData.Add(parsedLine);
            }
        }

        private void CheckForCombatEnd(long lineIndex)
        {
            if ((lineIndex - _linesAtCombatEnd > 10 || (lineIndex == _newNumberOfEntries - 1)) && _combatEnding)
            {
                _isInCombat = false;
                _combatEnding = false;
                
                CombatStopped(_currentCombatData);
                _currentCombatData.Clear();
            }
        }
    }
}
