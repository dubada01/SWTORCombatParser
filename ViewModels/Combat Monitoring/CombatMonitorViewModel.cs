using Microsoft.Win32;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.resources;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.Views;
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
        private static bool _liveParseActive;
        private CombatLogStreamer _combatLogStreamer;
        private int _numberOfSelectedCombats = 0;
        private bool showTrash;
        private List<EncounterCombat> _allEncounters = new List<EncounterCombat>();
        private bool _usingHistoricalData = true;
        private object combatAddLock = new object();
        private HistoricalRangeSelectionViewModel _historicalRangeVM;


        public event Action<bool> OnMonitoringStateChanged = delegate { };
        public event Action<List<Combat>> OnHistoricalCombatsParsed = delegate { };
        public event Action<Combat> OnCombatSelected = delegate { };
        public event Action<Combat> OnCombatUnselected = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };
        public event Action<Combat> LiveCombatFinished = delegate { };
        public event Action<string> OnNewLog = delegate { };
        public event Action<List<Entity>> ParticipantsUpdated = delegate { };
        public event Action<Entity> LocalPlayerId = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public string CurrentlySelectedLogName { get; set; }
        public bool ShowTrash
        {
            get => showTrash; set
            {
                showTrash = value;
                UpdateTrashVisibility();
            }
        }
        public ObservableCollection<EncounterCombat> PastEncounters { get; set; } = new ObservableCollection<EncounterCombat>();
        public EncounterCombat CurrentEncounter;
        public HistoricalRangeWiget HistoricalRange { get; set; }
        public bool LiveParseActive
        {
            get => _liveParseActive; set
            {

                _liveParseActive = value;
                OnPropertyChanged();
            }
        }
        public static bool IsLiveParseActive()
        {
            return _liveParseActive;
        }
        public CombatMonitorViewModel()
        {
            HistoricalRange = new HistoricalRangeWiget();
            _historicalRangeVM = new HistoricalRangeSelectionViewModel();
            _historicalRangeVM.HistoricalCombatsParsed += OnNewHistoricalCombats;
            HistoricalRange.DataContext = _historicalRangeVM;

            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.LocalPlayerIdentified += LocalPlayerFound;
            CombatLogStreamer.HistoricalLogsFinished += HistoricalLogsFinished;
            Observable.FromEvent<CombatStatusUpdate>(
                manager => CombatLogStreamer.CombatUpdated += manager,
                manager => CombatLogStreamer.CombatUpdated -= manager).Subscribe(update => NewCombatStatusAlert(update));
        }
        private void OnNewHistoricalCombats(List<Combat> historicalCombats)
        {
            OnHistoricalCombatsParsed(historicalCombats);
        }
        private void LocalPlayerFound(Entity obj)
        {
            LocalPlayerId(obj);
        }

        public void Reset()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _usingHistoricalData = true;
                PastEncounters.Clear();
                CurrentEncounter = null;
                _totalLogsDuringCombat.Clear();
                CombatLogStateBuilder.ClearState();
                ClearCombats();
            });
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
        public void EnableLiveParse(bool runningInBackground = false)
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
                    MonitorMostRecentLog(runningInBackground);
                }
            });
        }
        private void MonitorMostRecentLog(bool runningInBackground)
        {
            if(!runningInBackground)
                LoadingWindowFactory.ShowLoading();
            OnMonitoringStateChanged(true);
            var mostRecentLog = CombatLogLoader.GetMostRecentLogPath();
            //var mostRecentLog = Path.Join(_logPath, "test.txt");
            //File.Delete(mostRecentLog);
            //File.Create(mostRecentLog).Close();
            _combatLogStreamer.MonitorLog(mostRecentLog);
            OnNewLog("Started Monitoring: " + mostRecentLog);
            //Task.Run(() =>
            //{
            //    TransferLogData(mostRecentLog);
            //    File.Delete(mostRecentLog);
            //});

        }
        //TEST CODE
        private string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        private void TransferLogData(string testLogPath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(1252);
            var testFilesPath = @"C:\Users\duban\source\GameDevRepos\Orbs\SWTORCombatParser\TestCombatLogs";
            var files = Directory.EnumerateFiles(testFilesPath);
            foreach(var file in files)
            {
                using (var reader = new StreamReader(file, encoding))
                {
                    using (var fs = new FileStream(testLogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        while (!reader.EndOfStream)
                        {
                            var numberOfLines = new Random().Next(10, 30);
                            List<string> lines = new List<string>();
                            for (int i = 0; i < numberOfLines; i++)
                            {
                                lines.Add(reader.ReadLine() + "\r\n");
                            }

                            var stringBytes = encoding.GetBytes(string.Join("", lines));
                            fs.Write(stringBytes);
                            fs.Flush();


                            Thread.Sleep(100);
                        }
                        fs.Flush();
                        fs.Close();
                    }
                    reader.Close();
                }
            }


        }
        ///
        public void DisableLiveParse()
        {
            if (!LiveParseActive)
                return;
            LiveParseActive = false;
            _combatLogStreamer.StopMonitoring();
            OnMonitoringStateChanged(false);

            OnNewLog("Stopped Monitoring");
        }
        public void ClearCombats()
        {
            _numberOfSelectedCombats = 0;
            _allEncounters.Clear();
        }


        public ICommand LoadSpecificLogCommand => new CommandHandler(LoadSpecificLog);
        private void LoadSpecificLog(object test)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Combat Logs (*.txt)|*.txt";
            if (!CombatLogLoader.CheckIfCombatLoggingPresent())
            {
                OnNewLog("Failed to locate combat log folder: " + CombatLogLoader.GetLogDirectory());
                openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            else
            {
                openFileDialog.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
            }
            
            
            if (openFileDialog.ShowDialog() == true)
            {
                OnMonitoringStateChanged(false);
                CurrentlySelectedLogName = openFileDialog.FileName;
                OnPropertyChanged("CurrentlySelectedLogName");
                var logInfo = CombatLogLoader.LoadSpecificLog(CurrentlySelectedLogName);
                _combatLogStreamer.StopMonitoring();
                LiveParseActive = false;
                Reset();
                LoadingWindowFactory.ShowLoading();
                _combatLogStreamer.ParseCompleteLog(logInfo.Path);
            }
        }
        private void UpdateVisibleEncounters()
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                var orderedEncouters = _allEncounters.Where(e=>e.EncounterCombats.Any()).OrderByDescending(e => e.EncounterCombats.First().CombatStartTime);
                if (!orderedEncouters.Any())
                    return;
                orderedEncouters.First().Expand();
                PastEncounters = new ObservableCollection<EncounterCombat>(orderedEncouters);
                UpdateTrashVisibility();
                OnPropertyChanged("PastEncounters");
            });
        }

        private void CombatStarted(DateTime startTime, string location)
        {
            //reset leaderboards and overlays
            CombatIdentifier.NotifyNewCombatStarted();
            TryAddEncounter(startTime);
            if (!LiveParseActive)
                return;
            AddOngoingCombat(location);
            UpdateVisibleEncounters();
            _totalLogsDuringCombat[startTime] = new List<ParsedLogEntry>();
        }

        private void CombatUpdated(List<ParsedLogEntry> obj, DateTime combatStartTime)
        {
            if (!_totalLogsDuringCombat.ContainsKey(combatStartTime))
            {
                _totalLogsDuringCombat[combatStartTime] = new List<ParsedLogEntry>();
            }
            _totalLogsDuringCombat[combatStartTime].AddRange(obj);
            _usingHistoricalData = false;
            var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(_totalLogsDuringCombat[combatStartTime].ToList());
            CombatIdentifier.UpdateOverlays(combatInfo);
            ParticipantsUpdated(combatInfo.CharacterParticipants);
            if (CurrentEncounter == null)
                return;
            var combatUI = CurrentEncounter.UpdateOngoing(combatInfo);
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
                CurrentEncounter.RemoveOngoing();
                var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(obj);
                CombatIdentifier.FinalizeOverlays(combatInfo);
                //LocalCombatLogCaching.SaveCombatLogs(combatInfo, true);
                LiveCombatFinished(combatInfo);
                if (_totalLogsDuringCombat.ContainsKey(combatStartTime))
                {
                    _totalLogsDuringCombat.TryRemove(combatStartTime, out var t);
                }
                AddCombatToEncounter(combatInfo,true);
                if (combatInfo.IsCombatWithBoss)
                {
                    BossMechanicInfoSkimmer.AddBossInfoAfterCombat(combatInfo);
                    Leaderboards.TryAddLeaderboardEntry(combatInfo);
                }
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
                //LocalCombatLogCaching.SaveCombatLogs(combatInfo, false);
                TryAddEncounter(combatInfo.StartTime);
                AddCombatToEncounter(combatInfo,false);
            }
        }
        private void HistoricalLogsFinished()
        {
            GenerateHistoricalCombats();
            LoadingWindowFactory.HideLoading();
            _numberOfSelectedCombats = 0;
            _usingHistoricalData = false;
            UpdateVisibleEncounters();
        }
        private void TryAddEncounter(DateTime time)
        {
            var currentActiveEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(time);
            if (CurrentEncounter == null || CurrentEncounter.Info.Name != currentActiveEncounter.Name || CurrentEncounter.Info.Difficutly != currentActiveEncounter.Difficutly || CurrentEncounter.Info.NumberOfPlayer != currentActiveEncounter.NumberOfPlayer)
            {
                var newEncounter = new EncounterCombat
                {
                    Info = currentActiveEncounter,
                    Combats = new ObservableCollection<Combat>() { }
                };
                newEncounter.PastCombatSelected += SelectCombat;
                newEncounter.PastCombatUnselected += UnselectCombat;
                newEncounter.UnselectAll += UnSelectAll;
                _allEncounters.Add(newEncounter);
                _allEncounters.ForEach(e => e.Collapse());
                CurrentEncounter = newEncounter;
            }
        }
        private void AddCombatToEncounter(Combat combat, bool isRealtime)
        {
            CurrentEncounter.AddCombat(combat, isRealtime);
        }
        private void AddOngoingCombat(string location)
        {
           UnSelectAll();
           CurrentEncounter.AddOngoingCombat(location);
        }
        private void UpdateTrashVisibility()
        {
            foreach (var encounter in PastEncounters)
            {
                if (ShowTrash)
                    encounter.ShowTrash();
                else
                    encounter.HideTrash();
            }
        }
        private void UnSelectAll()
        {
            foreach (var combat in PastEncounters.SelectMany(e => e.EncounterCombats).Where(c => c.IsSelected))
            {
                combat.IsSelected = false;
            }
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
            ParticipantsUpdated(selectedCombat.Combat.CharacterParticipants);
            OnNewLog("Displaying new combat: " + selectedCombat.CombatLabel);
            OnCombatSelected(selectedCombat.Combat);
            
            CombatSelectionMonitor.FireNewCombat(selectedCombat.Combat);
            
        }
        private void NewCombatStatusAlert(CombatStatusUpdate update)
        {
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
