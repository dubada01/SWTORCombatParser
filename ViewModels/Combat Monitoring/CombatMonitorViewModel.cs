using Microsoft.Win32;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels
{
    public class CombatMonitorViewModel : INotifyPropertyChanged
    {
        private ConcurrentDictionary<DateTime, List<ParsedLogEntry>> _totalLogsDuringCombat = new ConcurrentDictionary<DateTime, List<ParsedLogEntry>>();
        private bool _liveParseActive;
        private CombatLogStreamer _combatLogStreamer;
        private int _numberOfSelectedCombats = 0;
        private bool showTrash;
        private List<PastCombat> _allCombats = new List<PastCombat>();
        private bool _usingHistoricalData = true;
        private object combatAddLock = new object();


        public event Action OnMonitoringStarted = delegate { };
        public event Action<Combat> OnCombatSelected = delegate { };
        public event Action<Combat> OnCombatUnselected = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };
        public event Action<string> OnNewLog = delegate { };
        public event Action<List<Entity>> ParticipantsUpdated = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public string CurrentlySelectedLogName { get; set; }
        public bool ShowTrash
        {
            get => showTrash; set
            {
                showTrash = value;
                UnSelectAll();
                UpdateVisibleData();
            }
        }
        public ObservableCollection<PastCombat> PastCombats { get; set; } = new ObservableCollection<PastCombat>();

        public ObservableCollection<EncounterCombat> PastEncounters { get; set; } = new ObservableCollection<EncounterCombat>();
        public EncounterCombat CurrentEncounter;

        public bool LiveParseActive
        {
            get => _liveParseActive; set
            {

                _liveParseActive = value;
                OnPropertyChanged();
            }
        }

        public CombatMonitorViewModel()
        {
            _combatLogStreamer = new CombatLogStreamer();

            _combatLogStreamer.HistoricalLogsFinished += HistoricalLogsFinished;
            Observable.FromEvent<CombatStatusUpdate>(
                manager => _combatLogStreamer.CombatUpdated += manager,
                manager => _combatLogStreamer.CombatUpdated -= manager).Subscribe(update => NewCombatStatusAlert(update));
        }


        public void Reset()
        {
            _usingHistoricalData = true;
            PastEncounters.Clear();
            PastCombats.Clear();
            CurrentEncounter = null;
            _totalLogsDuringCombat.Clear();
            CombatLogStateBuilder.ClearState();
            ClearCombats();
        }
        public ICommand ToggleLiveParseCommand => new CommandHandler(ToggleLiveParse);

        private void ToggleLiveParse(object test)
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
        private void EnableLiveParse()
        {
            if (!CombatLogLoader.CheckIfCombatLoggingPresent())
            {
                OnNewLog("Failed to locate combat log folder: " + CombatLogLoader.GetLogDirectory());
                return;
            }
            CurrentlySelectedLogName = "";
            OnPropertyChanged("CurrentlySelectedLogName");
            if (LiveParseActive)
                return;            
            Reset();
            LiveParseActive = true;
            
            Task.Run(() => {
                while (!CombatLogLoader.CheckIfCombatLogsArePresent() && LiveParseActive)
                {
                    Thread.Sleep(100);
                }
                if (!LiveParseActive)
                    return;
                else
                {
                    MonitorMostRecentLog();
                }
            });
        }
        private void MonitorMostRecentLog()
        {

            OnMonitoringStarted();
            var mostRecentLog = CombatLogLoader.GetMostRecentLogPath();
            //var mostRecentLog = @"C:\Users\duban\Documents\Star Wars - The Old Republic\CombatLogs\test.txt";
            //File.Create(mostRecentLog).Close();
            _combatLogStreamer.MonitorLog(mostRecentLog);
            OnNewLog("Started Monitoring: " + mostRecentLog);
            //Task.Run(() =>
            //{
            //    TransferLogData(mostRecentLog);
            //});
        }
        //TEST CODE
        private string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        private void TransferLogData(string testLogPath)
        {

            var logLines = File.ReadAllLines(Path.Combine(_logPath, "combat_2021-07-11_19_01_39_463431.txt"), new UTF7Encoding());
            using (var fs = new FileStream(testLogPath, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                var logIndex = 0;
                while (logIndex < logLines.Length)
                {
                    var logsToMove = Math.Min(logLines.Length - logIndex, new Random().Next(1, 30));
                    for (var i = 0; i < logsToMove; i++)
                    {
                        var stringBytes = new UTF7Encoding(true).GetBytes(logLines[logIndex + i] + '\n');
                        fs.Write(stringBytes);
                        fs.Flush();
                    }
                    logIndex += logsToMove;
                    Thread.Sleep(5);
                }
            }
        }
        ///
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
            _numberOfSelectedCombats = 0;
            _allCombats.Clear();
        }
        public void UnSelectAll()
        {
            foreach (var combat in PastCombats.Where(c => c.IsSelected))
            {
                combat.IsSelected = false;
            }
        }
        
        public ICommand LoadSpecificLogCommand => new CommandHandler(LoadSpecificLog);
        private void LoadSpecificLog(object test)
        {
            if (!CombatLogLoader.CheckIfCombatLoggingPresent())
            {
                OnNewLog("Failed to locate combat log folder: " + CombatLogLoader.GetLogDirectory());
                return;
            }
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
            if (openFileDialog.ShowDialog() == true)
            {
                OnMonitoringStarted();
                CurrentlySelectedLogName = openFileDialog.FileName;
                OnPropertyChanged("CurrentlySelectedLogName");
                var logInfo = CombatLogLoader.LoadSpecificLog(CurrentlySelectedLogName);
                _combatLogStreamer.StopMonitoring();
                LiveParseActive = false;
                Reset();
                _combatLogStreamer.ParseCompleteLog(logInfo.Path);
            }
        }
        private void UpdateVisibleData()
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                if (!ShowTrash)
                    PastCombats = new ObservableCollection<PastCombat>(_allCombats.Where(c => c.CombatLabel.Contains("{") || c.EncounterInfo != null || c.IsCurrentCombat || c.IsMostRecentCombat));
                else
                    PastCombats = new ObservableCollection<PastCombat>(_allCombats);
                OnPropertyChanged("PastCombats");
            });
        } 

        private void CombatStarted(DateTime startTime, string location)
        {
            CombatIdentifier.NotifyNewCombatStarted();
            if (!LiveParseActive)
                return;
            
            AddOngoingCombat(location);

            _totalLogsDuringCombat[startTime] = new List<ParsedLogEntry>();
        }
       
        private void CombatUpdated(List<ParsedLogEntry> obj, DateTime combatStartTime)
        {
            _totalLogsDuringCombat[combatStartTime].AddRange(obj);
            _usingHistoricalData = false;
            var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(_totalLogsDuringCombat[combatStartTime].ToList());
            CombatIdentifier.UpdateOverlays(combatInfo);
           
            var combatUI = _allCombats.FirstOrDefault(c => c.IsCurrentCombat);
            if (combatUI == null)
                return;
            combatUI.Combat = combatInfo;
            if (combatUI.IsSelected)
            {
                OnLiveCombatUpdate(combatInfo);
            }
        }
        private void CombatStopped(List<ParsedLogEntry> obj, DateTime combatStartTime)
        {
            if (obj.Count == 0)
                return;


            _totalLogsDuringCombat[combatStartTime] = obj;

            if (!_usingHistoricalData)
            {
                RemoveCombatInProgress();
                var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(obj);
                if (_totalLogsDuringCombat.ContainsKey(combatStartTime))
                {
                    _totalLogsDuringCombat.TryRemove(combatStartTime, out var t);
                }
                AddFinishedCombat(combatInfo);
                CombatIdentifier.UpdateOverlays(combatInfo);
                if (combatInfo.IsEncounterBoss)
                    Leaderboards.TryAddLeaderboardEntry(combatInfo);
            }

        }
        private void GenerateHistoricalCombats()
        {
            foreach (var combat in _totalLogsDuringCombat.Keys.OrderBy(t => t))
            {
                var combatLogs = _totalLogsDuringCombat[combat].ToList();
                if (combatLogs.Count == 0)
                    continue;
                var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(combatLogs);
                AddFinishedCombat(combatInfo);
            }
        }
        private void AddFinishedCombat(Combat combat)
        {
            AddCombat(combat);
            ManageEncounter(combat);
        }
        private void HistoricalLogsFinished()
        {
            GenerateHistoricalCombats();
            if(CurrentEncounter!=null)
                AddCombat(CurrentEncounter.OverallCombat, true);
            _numberOfSelectedCombats = 0;
            _usingHistoricalData = false;
            UpdateVisibleData();
        }
        private void ManageEncounter(Combat combat)
        {
            if (CurrentEncounter == null || CurrentEncounter.Info.Name != combat.ParentEncounter.Name)
            {
                var newEncounter = new EncounterCombat
                {
                    Info = combat.ParentEncounter,
                    Combats = new List<Combat>() { }
                };
                PastEncounters.Add(newEncounter);
                if(CurrentEncounter!=null)
                    AddCombat(CurrentEncounter.OverallCombat, true);
                CurrentEncounter = newEncounter;
            }
            CurrentEncounter.AddCombat(combat);

        }
        private void AddOngoingCombat(string location)
        {
            UnSelectAll();
            var combatUI = new PastCombat() { CombatLabel = location + " ongoing...", IsSelected = true, IsCurrentCombat = true, CombatStartTime = DateTime.Now };
            combatUI.PastCombatUnSelected += UnselectCombat;
            combatUI.PastCombatSelected += SelectCombat;
            combatUI.UnselectAll += UnSelectAll;

            _allCombats.Insert(0, combatUI);
            UpdateVisibleData();

        }

        public void AddCombat(Combat combatInfo, bool isEncounter = false)
        {
            lock (combatAddLock)
            {
                RemoveStaleEncounterCombat(isEncounter);

                if (_allCombats.Any(pc => pc.CombatStartTime == combatInfo.StartTime))
                    return;

                var combatUI = new PastCombat()
                {
                    Combat = combatInfo,
                    EncounterInfo = isEncounter ? CurrentEncounter.Info : null,
                    CombatLabel = GetCombatLabel(combatInfo, isEncounter),
                    CombatDuration = combatInfo.DurationSeconds.ToString("#,##0.0"),
                    CombatStartTime = combatInfo.StartTime
                };
                combatUI.PastCombatSelected += SelectCombat;
                combatUI.PastCombatUnSelected += UnselectCombat;
                combatUI.UnselectAll += UnSelectAll;
                _allCombats.Insert(0, combatUI);
                if (!isEncounter)
                {
                    _allCombats.ForEach(c => c.IsMostRecentCombat = false);
                    combatUI.IsMostRecentCombat = true;
                }
                if (!_usingHistoricalData)
                {
                    UpdateVisibleData();
                    if (!isEncounter)
                    {
                        UnSelectAll();
                        combatUI.IsSelected = true;
                    }
                }
                OnNewLog("Combat with duration " + combatInfo.DurationSeconds + " ended");
            }
        }

        private void RemoveStaleEncounterCombat(bool isEncounter)
        {
            if (isEncounter)
            {
                var encounterToUpdate = _allCombats.FirstOrDefault(pc => pc.EncounterInfo == CurrentEncounter.Info);
                _allCombats.Remove(encounterToUpdate);
                if (!_usingHistoricalData)
                    UpdateVisibleData();
            }
        }

        private void RemoveCombatInProgress()
        {
            lock (combatAddLock)
            {
                var ongoingCombat = _allCombats.FirstOrDefault(c => c.IsCurrentCombat);
                if (ongoingCombat == null)
                    return;
                ongoingCombat.IsSelected = false;
                _allCombats.Remove(ongoingCombat);
                UpdateVisibleData();
            }

        }

        private string GetCombatLabel(Combat combat, bool isEncounter)
        {
            if (!isEncounter)
                return combat.EncounterBossInfo == "" ? string.Join(", ", combat.Targets.Select(t=>t.Name).Where(t => !string.IsNullOrEmpty(t))) : combat.EncounterBossInfo;
            else
                return combat.ParentEncounter.Name;
        }
        private void UnselectCombat(PastCombat unslectedCombat)
        {
            _numberOfSelectedCombats--;
            _numberOfSelectedCombats = Math.Max(_numberOfSelectedCombats, 0);
            if (unslectedCombat.Combat == null)
                return;
            OnNewLog("Removing combat: " + unslectedCombat.CombatLabel + " from plot.");
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
            OnNewLog("Displaying new combat: " + selectedCombat.CombatLabel);
            OnCombatSelected(selectedCombat.Combat);
            
            CombatSelectionMonitor.FireNewCombat(selectedCombat.Combat);
            ParticipantsUpdated(selectedCombat.Combat.CharacterParticipants);
        }
        private void NewCombatStatusAlert(CombatStatusUpdate update)
        {
            Trace.WriteLine($"{DateTime.Now.ToString("HH:mm:ss.ffff")}: {update.Type}");
            switch (update.Type)
            {
                case UpdateType.Start:
                    CombatStarted(update.CombatStartTime, update.CombatLocation);
                    break;
                case UpdateType.Stop:
                    CombatStopped(update.Logs.ToList(), update.CombatStartTime);
                    break;
                case UpdateType.Update:
                    CombatUpdated(update.Logs.ToList(), update.CombatStartTime);
                    break;
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
