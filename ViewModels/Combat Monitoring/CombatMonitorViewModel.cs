using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace SWTORCombatParser.ViewModels
{
    public class CombatMonitorViewModel:INotifyPropertyChanged
    {
        private List<ParsedLogEntry> _totalLogsDuringCombat = new List<ParsedLogEntry>();
        
        private CombatLogStreamer _combatLogStreamer;
        public CombatMonitorViewModel()
        {
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStopped += CombatStopped;
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
            
        }
        public event Action<Combat> OnNewCombat = delegate { };
        public event Action<string> OnCharacterNameIdentified = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand StartLiveParseCommand => new CommandHandler(StartLiveParse, () => true);
        public ObservableCollection<PastCombat> PastCombats { get; set; } = new ObservableCollection<PastCombat>();
        private void StartLiveParse()
        {
            var mostRecentLog = CombatLogLoader.LoadMostRecentLog();
            _combatLogStreamer.MonitorLog(mostRecentLog.Path);
        }
        private void UpdateLog(List<ParsedLogEntry> obj)
        {
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            OnNewCombat(combatInfo);
            OnCharacterNameIdentified(combatInfo.CharacterName);
        }
        private void CombatStarted(string characterName)
        {
            foreach (var combat in PastCombats)
                combat.Reset();
            OnCharacterNameIdentified(characterName);
            var combatUI = new PastCombat() { CombatLabel = "Ongoing..." };
            combatUI.PastCombatSelected += SelectCombat;
            App.Current.Dispatcher.Invoke(delegate {
                PastCombats.Insert(0, combatUI);
            });
            _totalLogsDuringCombat.Clear();
        }
        private void CombatStopped(List<ParsedLogEntry> obj)
        {
            if(PastCombats.Any(c=>c.CombatLabel == "Ongoing..."))
                App.Current.Dispatcher.Invoke(delegate {
                    PastCombats.Remove(PastCombats.First(c=>c.CombatLabel == "Ongoing..."));
                });
            if (obj.Count == 0)
                return;
            _totalLogsDuringCombat.Clear();
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            var combatUI = new PastCombat() { Combat = combatInfo, CombatLabel = string.Join(", ", combatInfo.Targets), CombatDuration = combatInfo.DurationSeconds.ToString() };
            combatUI.PastCombatSelected += SelectCombat;
            App.Current.Dispatcher.Invoke(delegate {
                PastCombats.Insert(0, combatUI);
            });
            PastCombats[0].SelectCombat();
            _totalLogsDuringCombat.Clear();
            //var logState = CombatLogParser.BuildLogState(CombatLogLoader.LoadSpecificLog(System.IO.Path.Join("TestCombatLogs", _currentLogName)));
            //var abilities = logState.Modifiers.Select(m => m.Name).Distinct();
            //var durations = logState.Modifiers.Where(m=>m.StartTime >= obj.First().TimeStamp && m.StopTime < obj.Last().TimeStamp).GroupBy(v => v.Name, v => v.DurationSeconds, (name, durations) => new { Name = name, SumOfDurations = durations.Sum(), CountOfAbilities = durations.Count() }).OrderByDescending(effect => effect.SumOfDurations);
            //Trace.WriteLine("------ABILITY DURATIONS------");
            //Trace.WriteLine(combatInfo.DurationSeconds);
            //durations.ToList().ForEach(a => Trace.WriteLine("Ability: " + a.Name + " Duration: " + a.SumOfDurations+ " Number of: "+a.CountOfAbilities));
        }
        private void SelectCombat(PastCombat selectedCombat)
        {
            foreach (var combat in PastCombats)
                combat.Reset();

            OnNewCombat(selectedCombat.Combat);
            OnCharacterNameIdentified(selectedCombat.Combat.CharacterName);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
