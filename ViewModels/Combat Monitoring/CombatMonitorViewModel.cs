using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.SoftwareLogging;
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
        public event Action<Combat> OnCombatSelected = delegate { };
        public event Action<Combat> OnCombatUnselected = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };

        public event Action<string> OnNewLog = delegate { };
        public event Action<string> OnCharacterNameIdentified = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand StartLiveParseCommand => new CommandHandler(StartLiveParse, () => true);
        public ObservableCollection<PastCombat> PastCombats { get; set; } = new ObservableCollection<PastCombat>();
        private int _numberOfSelectedCombats = 0;
        private void StartLiveParse()
        {
            var mostRecentLog = CombatLogLoader.LoadMostRecentLog();
            var testLog = CombatLogLoader.LoadSpecificLog(@"C:\Users\duban\source\GameDevRepos\SWTORCombatParser\TestCombatLogs\combat_2021-07-11_19_01_39_463431.txt");
            _combatLogStreamer.MonitorLog(testLog.Path);
            OnNewLog("Started Monitoring: " + testLog.Path);
        }
        private void UpdateLog(List<ParsedLogEntry> obj)
        {
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            OnLiveCombatUpdate(combatInfo);
            OnCharacterNameIdentified(combatInfo.CharacterName);
        }
        private void CombatStarted(string characterName, string location)
        {
            if (PastCombats.Any(pc => pc.CombatLabel == location + " ongoing..."))
                return;

            OnCharacterNameIdentified(characterName);
            var combatUI = new PastCombat() { CombatLabel = location + " ongoing..." };
            combatUI.PastCombatSelected += SelectCombat;
            App.Current.Dispatcher.Invoke(delegate {
                PastCombats.Insert(0, combatUI);
            });
            _totalLogsDuringCombat.Clear();
            OnNewLog("Detected Combat For: " + characterName +" in "+location);
        }
        private void CombatStopped(List<ParsedLogEntry> obj)
        {
            if(PastCombats.Any(c=>c.CombatLabel.Contains("ongoing...")))
                App.Current.Dispatcher.Invoke(delegate {
                    PastCombats.Remove(PastCombats.First(c=>c.CombatLabel.Contains("ongoing...")));
                });
            if (obj.Count == 0)
                return;
            _totalLogsDuringCombat.Clear();
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            var combatUI = new PastCombat() { Combat = combatInfo, CombatLabel = combatInfo.RaidBossInfo == ""?string.Join(", ", combatInfo.Targets):combatInfo.RaidBossInfo, CombatDuration = combatInfo.DurationSeconds.ToString() };
            combatUI.PastCombatSelected += SelectCombat;
            combatUI.PastCombatUnSelected += UnselectCombat;
            App.Current.Dispatcher.Invoke(delegate {
                PastCombats.Insert(0, combatUI);
            });
            OnNewLog("Combat with duration " + combatInfo.DurationSeconds +" ended");
            _totalLogsDuringCombat.Clear();
        }
        private void UnselectCombat(PastCombat unslectedCombat)
        {
            _numberOfSelectedCombats--;
            OnNewLog("Removing combat: " + unslectedCombat.CombatLabel +" from plot.");
            OnCombatUnselected(unslectedCombat.Combat);
        }
        private void SelectCombat(PastCombat selectedCombat)
        {
            _numberOfSelectedCombats++;
            if (_numberOfSelectedCombats > 3)
            {
                selectedCombat.IsSelected = false;
                return;
            }
            OnNewLog("Displaying new combat: "+selectedCombat.CombatLabel);
            OnCombatSelected(selectedCombat.Combat);
            OnCharacterNameIdentified(selectedCombat.Combat.CharacterName);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
