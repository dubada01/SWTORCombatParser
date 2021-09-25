using Microsoft.Win32;
using SWTORCombatParser.DataStructures.RaidInfos;
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
using System.Threading;
using System.Threading.Tasks;
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
    public class EncounterCombat
    {
        public EncounterInfo Info { get; set; }
        public Combat OverallCombat => GetOverallCombat();
        public List<Combat> Combats { get; set; }
        private Combat GetOverallCombat()
        {
            var overallCombat = CombatIdentifier.GenerateNewCombatFromLogs(Combats.SelectMany(c => c.Logs.SelectMany(kvp=>kvp.Value)).ToList());
            overallCombat.StartTime = overallCombat.StartTime.AddSeconds(-1);
            return overallCombat;
        }
    }
    public class CombatMonitorViewModel : INotifyPropertyChanged
    {
        private List<ParsedLogEntry> _totalLogsDuringCombat = new List<ParsedLogEntry>();
        private bool _liveParseActive;
        private CombatLogStreamer _combatLogStreamer;
        private int _numberOfSelectedCombats = 0; 
        private bool showTrash;
        private List<PastCombat> _allCombats = new List<PastCombat>();
        private bool usingHistoricalData = false;
        public CombatMonitorViewModel()
        {
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.CombatStopped += CombatStopped;
            _combatLogStreamer.CombatStarted += CombatStarted;
            _combatLogStreamer.NewLogEntries += UpdateLog;
            _combatLogStreamer.HistoricalLogsFinished += HistoricalLogsFinished;
        }

        public event Action OnMonitoringStarted = delegate { };
        public event Action<Combat> OnCombatSelected = delegate { };
        public event Action<Combat> OnCombatUnselected = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };
        public event Action<string> OnNewLog = delegate { };
        public event Action<List<Entity>> ParticipantsUpdated = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public bool ShowTrash { get => showTrash; set
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
            if (LiveParseActive)
                return;
            LiveParseActive = true;
            CombatLogStateBuilder.ClearState();
            ClearCombats();
            OnMonitoringStarted();
            var mostRecentLog = CombatLogLoader.GetMostRecentLogPath();
            //var mostRecentLog = @"C:\Users\David\source\repos\SWTORCombatParser\SWTORCombatParser_Test\bin\Debug\netcoreapp3.1\testLog.txt";
            //File.Create(mostrecentLog).Close();
            _combatLogStreamer.MonitorLog(mostRecentLog);
            OnNewLog("Started Monitoring: " + mostRecentLog);
            //Task.Run(() => {
            //    TransferLogData(mostrecentLog);
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
            App.Current.Dispatcher.Invoke(() =>
            {
                _numberOfSelectedCombats = 0;
                _allCombats.Clear();
            });

        }
        public void UnSelectAll()
        {
            foreach (var combat in PastCombats.Where(c => c.IsSelected))
            {
                combat.IsSelected = false;
            }
        }
        public string CurrentlySelectedLogName { get; set; }
        public ICommand LoadSpecificLogCommand => new CommandHandler(LoadSpecificLog);
        private void LoadSpecificLog(object test)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
            if (openFileDialog.ShowDialog() == true)
            {
                OnMonitoringStarted();
                _allCombats.Clear();
                CurrentlySelectedLogName = openFileDialog.FileName;
                OnPropertyChanged("CurrentlySelectedLogName");
                var logInfo = CombatLogLoader.LoadSpecificLog(CurrentlySelectedLogName);
                _combatLogStreamer.StopMonitoring(); 
                LiveParseActive = false;
                _combatLogStreamer.ParseCompleteLog(logInfo.Path);
            }
        }
        private void UpdateVisibleData()
        {
            if (!ShowTrash)
                PastCombats = new ObservableCollection<PastCombat>(_allCombats.Where(c => c.CombatLabel.Contains("{") || c.EncounterInfo != null || c.IsCurrentCombat || c.IsMostRecentCombat));
            else
                PastCombats = new ObservableCollection<PastCombat>(_allCombats);
            OnPropertyChanged("PastCombats");
        }

        private void CombatStarted(string characterName, string location, bool parsingOldLog)
        {
            CombatIdentifier.NotifyNewCombatStarted();
            if (!LiveParseActive || parsingOldLog)
                return;
            
            StartCombat(location);

            _totalLogsDuringCombat.Clear();
            OnNewLog("Detected Combat For: " + characterName + " in " + location);
        }
        private void StartCombat(string location)
        {
            UnSelectAll();
            var combatUI = new PastCombat() { CombatLabel = location + " ongoing...", IsSelected = true, IsCurrentCombat = true, CombatStartTime = DateTime.Now };
            combatUI.PastCombatUnSelected += UnselectCombat;
            combatUI.PastCombatSelected += SelectCombat;
            combatUI.UnselectAll += UnSelectAll;
            App.Current.Dispatcher.Invoke(delegate
            {
                _allCombats.Insert(0, combatUI);
                UpdateVisibleData();
            });
        }
        
        private void UpdateLog(List<ParsedLogEntry> obj)
        {
            _totalLogsDuringCombat.AddRange(obj);
            usingHistoricalData = true;
            var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(_totalLogsDuringCombat.ToList());
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
        private void CombatStopped(List<ParsedLogEntry> obj)
        {
            if (obj.Count == 0)
                return;
            RemoveCombatInProgress();
            _totalLogsDuringCombat.Clear();
            _totalLogsDuringCombat.AddRange(obj);
            var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(_totalLogsDuringCombat.ToList());
            if(usingHistoricalData)
                CombatIdentifier.UpdateOverlays(combatInfo);
            AddCombat(combatInfo);
            ManageEncounter(combatInfo);
        }
        private void HistoricalLogsFinished()
        {
            _numberOfSelectedCombats = 0;
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
                CurrentEncounter = newEncounter;
            }
            CurrentEncounter.Combats.Add(combat);
            AddCombat(CurrentEncounter.OverallCombat, true);
        }
        public void AddCombat(Combat combatInfo, bool isEncounter = false)
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
            if (usingHistoricalData)
            {
                App.Current.Dispatcher.Invoke(delegate
                {
                    UpdateVisibleData();
                });
            }
                
            if (usingHistoricalData && !isEncounter)
            {
                UnSelectAll();
                combatUI.IsSelected = true;
                usingHistoricalData = false;
            }
            OnNewLog("Combat with duration " + combatInfo.DurationSeconds + " ended");
        }

        private void RemoveStaleEncounterCombat(bool isEncounter)
        {
            if (isEncounter)
            {
                App.Current.Dispatcher.Invoke(delegate
                {
                    var encounterToUpdate = _allCombats.FirstOrDefault(pc => pc.EncounterInfo == CurrentEncounter.Info);
                    _allCombats.Remove(encounterToUpdate);
                    if(usingHistoricalData)
                        UpdateVisibleData();
                });
            }
        }

        private void RemoveCombatInProgress()
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                var ongoingCombat = _allCombats.FirstOrDefault(c => c.IsCurrentCombat);
                if (ongoingCombat == null)
                    return;
                ongoingCombat.IsSelected = false;
                _allCombats.Remove(ongoingCombat);
                UpdateVisibleData();
            });
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
