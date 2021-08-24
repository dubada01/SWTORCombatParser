using Microsoft.Win32;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.SoftwareLogging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace SWTORCombatParser.ViewModels
{
    public static class CombatSelectionMonitor
    {
        public static event Action<Combat> NewCombatSelected = delegate { };
        public static void FireNewCombat(Combat selectedCombat)
        {
            NewCombatSelected(selectedCombat);
        }
    }
    public class CombatMonitorViewModel:INotifyPropertyChanged
    {
        private List<ParsedLogEntry> _totalLogsDuringCombat = new List<ParsedLogEntry>();
        private bool _liveParseActive;
        private CombatLogStreamer _combatLogStreamer;
        private int _numberOfSelectedCombats = 0;
        public CombatMonitorViewModel()
        {
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStopped += CombatStopped;
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
        }

        public event Action OnMonitoringStarted = delegate { };
        public event Action<Combat> OnCombatSelected = delegate { };
        public event Action<Combat> OnCombatUnselected = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };
        public event Action<string> OnNewLog = delegate { };
        public event Action<string> OnCharacterNameIdentified = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<PastCombat> PastCombats { get; set; } = new ObservableCollection<PastCombat>();
        public bool LiveParseActive { get => _liveParseActive; set {

                _liveParseActive = value;
                OnPropertyChanged();
            } }
        public void RaidingStarted()
        {
            ClearCombats();
            EnableLiveParse(true);
        }
        public void RaidingStopped()
        {
            ClearCombats();
        }
        public ICommand ToggleLiveParseCommand => new CommandHandler(ToggleLiveParse, () => true);

        private void ToggleLiveParse()
        {
            if (!LiveParseActive)
            {
                EnableLiveParse();
            }
            else
            {
                DisableLiveParse();
            }
            
        }
        private void EnableLiveParse(bool forRaiding = false)
        {
            if (LiveParseActive)
                return;
            LiveParseActive = true;
            ClearCombats();
            OnMonitoringStarted();
            var mostRecentLog = CombatLogLoader.LoadMostRecentLog();
            _combatLogStreamer.MonitorLog(mostRecentLog.Path, forRaiding);
            OnNewLog("Started Monitoring: " + mostRecentLog.Path);
        }
        private void DisableLiveParse()
        {
            if (!LiveParseActive)
                return;
            LiveParseActive = false;
            _combatLogStreamer.StopMonitoring();
            OnNewLog("Stopped Monitoring");
        }
        public void ClearCombats()
        {
            App.Current.Dispatcher.Invoke(() => {
                _numberOfSelectedCombats = 0;
                PastCombats.Clear();
            });
            
        }
        public string CurrentlySelectedLogName { get; set; }
        public ICommand LoadSpecificLogCommand => new CommandHandler(LoadSpecificLog, () => true);
        private void LoadSpecificLog()
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
            if(openFileDialog.ShowDialog() == true)
            {
                OnMonitoringStarted();
                PastCombats.Clear();
                CurrentlySelectedLogName = openFileDialog.FileName;
                OnPropertyChanged("CurrentlySelectedLogName");
                var logInfo = CombatLogLoader.LoadSpecificLog(CurrentlySelectedLogName);
                _combatLogStreamer.StopMonitoring();
                _combatLogStreamer.MonitorLog(logInfo.Path);
            }
        }

        internal void AddRaidCombat(Combat combatAdded)
        {
            AddCombat(combatAdded);
        }

        private void UpdateLog(List<ParsedLogEntry> obj)
        {
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            var combatUI = PastCombats.First(c => c.IsCurrentCombat);

            combatUI.Combat = combatInfo;
            if (combatUI.IsSelected)
            {
                OnLiveCombatUpdate(combatInfo);
            }
        }
        public void StartCombat(string location)
        {
            foreach (var combat in PastCombats)
                combat.IsSelected = false;
            var combatUI = new PastCombat() { CombatLabel = location + " ongoing...", IsSelected = true, IsCurrentCombat = true, CombatStartTime = DateTime.Now };
            combatUI.PastCombatUnSelected += UnselectCombat;
            combatUI.PastCombatSelected += SelectCombat;
            App.Current.Dispatcher.Invoke(delegate {
                PastCombats.Insert(0, combatUI);
            });
        }
        private void CombatStarted(string characterName, string location)
        {
            if (!LiveParseActive)
                return;
            if (PastCombats.Any(pc => pc.IsCurrentCombat))
                return;
            OnCharacterNameIdentified(characterName);

            StartCombat(location);

            _totalLogsDuringCombat.Clear();
            OnNewLog("Detected Combat For: " + characterName +" in "+location);
        }

        private void CombatStopped(List<ParsedLogEntry> obj)
        {
            if (obj.Count == 0)
                return;
            _totalLogsDuringCombat.Clear();
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.ParseOngoingCombat(_totalLogsDuringCombat.ToList());
            AddCombat(combatInfo);
            _totalLogsDuringCombat.Clear();
        }

        public void AddCombat(Combat combatInfo)
        {
            if (PastCombats.Any(c => c.IsCurrentCombat))
                App.Current.Dispatcher.Invoke(delegate
                {
                    var ongoingCombat = PastCombats.First(c => c.IsCurrentCombat);
                    ongoingCombat.IsSelected = false;
                    PastCombats.Remove(ongoingCombat);
                });
            if (PastCombats.Any(pc => pc.CombatStartTime == combatInfo.StartTime))
                return;
            var combatUI = new PastCombat() { Combat = combatInfo, CombatLabel = combatInfo.RaidBossInfo == "" ? string.Join(", ", combatInfo.Targets) : combatInfo.RaidBossInfo, CombatDuration = combatInfo.DurationSeconds.ToString("#,##0.0"), CombatStartTime = combatInfo.StartTime };
            combatUI.PastCombatSelected += SelectCombat;
            combatUI.PastCombatUnSelected += UnselectCombat;
            App.Current.Dispatcher.Invoke(delegate
            {
                PastCombats.Insert(0, combatUI);
            });
            OnNewLog("Combat with duration " + combatInfo.DurationSeconds + " ended");
        }

        private void UnselectCombat(PastCombat unslectedCombat)
        {
            _numberOfSelectedCombats--;
            _numberOfSelectedCombats = Math.Max(_numberOfSelectedCombats, 0);
            if (unslectedCombat.Combat == null)
                return;
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
            CombatSelectionMonitor.FireNewCombat(selectedCombat.Combat);
            OnCharacterNameIdentified(selectedCombat.Combat.CharacterName);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
