using Microsoft.Win32;
using SWTORCombatParser.DataStructures;
using Prism.Commands;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.Views.Home_Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using static System.Windows.Forms.AxHost;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public class CombatMonitorViewModel : INotifyPropertyChanged
    {
        private ConcurrentDictionary<DateTime, List<ParsedLogEntry>> _totalLogsDuringCombat = new ConcurrentDictionary<DateTime, List<ParsedLogEntry>>();
        private static bool _liveParseActive;
        private static bool _autoParseEnabled;
        private CombatLogStreamer _combatLogStreamer;
        private int _numberOfSelectedCombats = 0;
        private bool showTrash;
        private List<EncounterCombat> _allEncounters = new List<EncounterCombat>();
        private bool _usingHistoricalData = true;
        private object combatAddLock = new object();
        private HistoricalRangeSelectionViewModel _historicalRangeVM;
        private bool _stubLogs;
        // private readonly int _linesPerWriteMin = 5560;
        private readonly int _linesPerWriteMin = 1560;

        public event Action<bool> OnMonitoringStateChanged = delegate { };
        public event Action<List<Combat>> OnHistoricalCombatsParsed = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };
        public event Action<double> OnNewLogTimeOffsetMs = delegate { };
        public event Action<double> OnNewTotalTimeOffsetMs = delegate { };
        public event Action<string> OnNewLog = delegate { };
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
        public double CurrentLogOffsetMs
        {
            get => currentLogOffsetMs; set
            {
                currentLogOffsetMs = value;
                OnPropertyChanged();
            }
        }
        public double CurrentOrbsOffsetMs => Math.Round(Math.Max(0, CurrentTotalOffsetMs - CurrentLogOffsetMs), 1);
        public double CurrentTotalOffsetMs
        {
            get => currentTotalOffsetMs; set
            {
                currentTotalOffsetMs = value;
                OnPropertyChanged("CurrentOrbsOffsetMs");
                OnPropertyChanged();
            }
        }
        public bool LiveParseActive
        {
            get => _liveParseActive; set
            {

                _liveParseActive = value;
                OnPropertyChanged("CurrentOrbsOffsetMs");
                OnPropertyChanged();
            }
        }
        public string AutoLiveParseText => _autoParseEnabled ? "Disable Auto Parse" : "Enable Auto Parse";
        public ICommand AutoLiveParseCommand => new DelegateCommand(AutoLiveParseToggle);

        private void AutoLiveParseToggle()
        {
            _autoParseEnabled = !_autoParseEnabled;
            Settings.WriteSetting("Auto_Parse", _autoParseEnabled);
            OnPropertyChanged("AutoLiveParseText");
        }

        public string GetActiveFile()
        {
            return _combatLogStreamer.CurrentLog;
        }
        public static bool IsLiveParseActive()
        {
            return _liveParseActive;
        }
        public CombatMonitorViewModel()
        {
            _stubLogs = Settings.ReadSettingOfType<bool>("stub_logs");
            _autoParseEnabled = Settings.ReadSettingOfType<bool>("Auto_Parse");
            HistoricalRange = new HistoricalRangeWiget();
            _historicalRangeVM = new HistoricalRangeSelectionViewModel();
            _historicalRangeVM.HistoricalCombatsParsed += OnNewHistoricalCombats;
            HistoricalRange.DataContext = _historicalRangeVM;

            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.NewLogTimeOffsetMs += UpdateLogOffset;
            _combatLogStreamer.NewTotalTimeOffsetMs += UpdateTotalOffset;
            _combatLogStreamer.LocalPlayerIdentified += LocalPlayerFound;
            CombatLogStreamer.HistoricalLogsFinished += HistoricalLogsFinished;
            Observable.FromEvent<CombatStatusUpdate>(
                manager => CombatLogStreamer.CombatUpdated += manager,
                manager => CombatLogStreamer.CombatUpdated -= manager).Subscribe(NewCombatStatusAlert);
            if (_autoParseEnabled)
                EnableLiveParse();
        }

        private void UpdateLogOffset(double offset)
        {
            OnNewLogTimeOffsetMs(offset);
        }
        private void UpdateTotalOffset(double offset)
        {
            OnNewTotalTimeOffsetMs(offset);
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
                UnSelectAll();
                _usingHistoricalData = true;
                PastEncounters.Clear();
                CurrentEncounter = null;
                _totalLogsDuringCombat.Clear();
                CombatIdentifier.CurrentCombat = new Combat();
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

            Task.Run(() =>
            {
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
            if (!runningInBackground)
                LoadingWindowFactory.ShowLoading();
            OnMonitoringStateChanged(true);
            var mostRecentLog = "";
            if (_stubLogs)
            {
#if DEBUG
                mostRecentLog = Path.Join(_logPath, "test.txt");
                File.Delete(mostRecentLog);
                File.Create(mostRecentLog).Close();
                //InitStubbedLog(mostRecentLog);
#else
                mostRecentLog = CombatLogLoader.GetMostRecentLogPath();
#endif

            }
            else
            {
                mostRecentLog = CombatLogLoader.GetMostRecentLogPath();
            }
            _combatLogStreamer.MonitorLog(mostRecentLog);
            OnNewLog("Started Monitoring: " + mostRecentLog);
            if (_stubLogs)
            {
#if DEBUG
                Task.Run(() =>
                {
                    Thread.Sleep(1000);
                    TransferLogData(mostRecentLog);
                    File.Delete(mostRecentLog);
                });
#endif
            }
        }
        //TEST CODE
        private string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        private double currentLogOffsetMs;
        private double currentTotalOffsetMs;
        private void TransferLogData(string testLogPath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(1252);
            var testFilesPath = @"C:\Users\duban\Desktop\TestLogs";
            var files = Directory.EnumerateFiles(testFilesPath);
            foreach (var file in files)
            {
                using (var reader = new StreamReader(file, encoding))
                {
                    //reader.Read(new char[75000000]);
                    using (var fs = new FileStream(testLogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    {
                        while (!reader.EndOfStream)
                        {
                            var delay = new Random().Next(25, 500);
                            var delayScalar = delay / 25d;
                            var numberOfLines = new Random().Next(_linesPerWriteMin, (int)(_linesPerWriteMin * 1.5)) * delayScalar;
                            char[] buffer = new char[(int)numberOfLines];
                            reader.Read(buffer);
                            var stringBytes = encoding.GetBytes(string.Join("", buffer));
                            fs.Write(stringBytes);
                            fs.Flush();


                            Thread.Sleep(delay);
                        }
                        fs.Flush();
                        fs.Close();
                    }
                    reader.Close();
                }
            }
        }
        private void InitStubbedLog(string testLogPath)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(1252);
            var testFilesPath = @"C:\Users\duban\source\dubatech-repos\SWTORCombatParser\SWTORCombatParser_Test\TestLogs";
            var files = Directory.EnumerateFiles(testFilesPath);
            var file = files.First();
            using (var reader = new StreamReader(file, encoding))
            {
                using (var fs = new FileStream(testLogPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    var initialLines = 75000000;
                    char[] initialBuffer = new char[(int)initialLines];
                    reader.Read(initialBuffer);
                    var initialString = encoding.GetBytes(string.Join("", initialBuffer));
                    fs.Write(initialString);
                    fs.Flush();
                    fs.Close();
                }
                reader.Close();
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
                var orderedEncouters = _allEncounters.Where(e => e.EncounterCombats.Any()).OrderByDescending(e => e.EncounterCombats.First().CombatStartTime);
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
            Leaderboards.Reset();
            TryAddEncounter(startTime);
            if (!LiveParseActive)
                return;
            Logging.LogInfo("NEW real time combat started at " + startTime.ToString());
            AddOngoingCombat(location);
            UpdateVisibleEncounters();
            _totalLogsDuringCombat[startTime] = new List<ParsedLogEntry>();
        }

        private void CombatUpdated(List<ParsedLogEntry> obj, DateTime combatStartTime)
        {
            _totalLogsDuringCombat[combatStartTime] = obj;
            _usingHistoricalData = false;
            var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(_totalLogsDuringCombat[combatStartTime].ToList(), true);
            if (combatInfo.IsCombatWithBoss)
            {
                Leaderboards.StartGetPlayerLeaderboardStandings(combatInfo);
                Leaderboards.StartGetTopLeaderboardEntries(combatInfo);
            }
            CombatSelectionMonitor.InProgressCombatSeleted(combatInfo);
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
                Logging.LogInfo("Real time combat started at " + combatStartTime.ToString() + " has STOPPED");
                CurrentEncounter?.RemoveOngoing();
                var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(obj, true, combatEndUpdate: true);
                //if (combatInfo.IsCombatWithBoss)
                //    Leaderboards.StartGetPlayerLeaderboardStandings(combatInfo);
                CombatSelectionMonitor.SelectCompleteCombat(combatInfo);
                if (_totalLogsDuringCombat.ContainsKey(combatStartTime))
                {
                    _totalLogsDuringCombat.TryRemove(combatStartTime, out var t);
                }
                AddCombatToEncounter(combatInfo, true);
                if (combatInfo.IsCombatWithBoss)
                {
                    Leaderboards.TryAddLeaderboardEntry(combatInfo);
                }
                if (combatInfo.WasBossKilled)
                {
                    Stats.RecordCombatState(combatInfo);
                }
            }

        }
        private void GenerateHistoricalCombats()
        {
            foreach (var combatStartTime in _totalLogsDuringCombat.Keys.OrderBy(t => t))
            {
                var combatLogs = _totalLogsDuringCombat[combatStartTime].ToList();
                if (combatLogs.Count == 0)
                    continue;
                Logging.LogInfo("Processing combat with start time " + combatStartTime + " and " + combatLogs.Count + " log entries");
                var combatInfo = CombatIdentifier.GenerateNewCombatFromLogs(combatLogs, false, true);
                Logging.LogInfo("Combat processed!");
                //LocalCombatLogCaching.SaveCombatLogs(combatInfo, false);
                var addedNewEncounter = TryAddEncounter(combatInfo.StartTime);
                Logging.LogInfo(addedNewEncounter ? "Added new encounter!" : "Adding to existing encounter");
                AddCombatToEncounter(combatInfo, false);
                Logging.LogInfo("Combat added to encounter");
            }
        }
        private void HistoricalLogsFinished(DateTime combatEndTime, bool localPlayerIdentified)
        {
            Logging.LogInfo("Processing logs into combats...");
            Logging.LogInfo("Detected " + _totalLogsDuringCombat.Keys.Count() + " distinct combats");
            GenerateHistoricalCombats();
            LoadingWindowFactory.HideLoading();
            _numberOfSelectedCombats = 0;
            _usingHistoricalData = false;
            UpdateVisibleEncounters();
            if (_allEncounters.Any())
            {
                _allEncounters.Last().EncounterCombats.First().AdditiveSelectionToggle();
                var combatSelected = _allEncounters.Last().EncounterCombats.First().Combat;
                CombatIdentifier.CurrentCombat = _allEncounters.Last().EncounterCombats.First().Combat;
                //if (combatSelected.IsCombatWithBoss)
                //{
                //    Leaderboards.StartGetPlayerLeaderboardStandings(combatSelected);
                //    Leaderboards.StartGetTopLeaderboardEntries(combatSelected);
                //}
            }
        }
        private bool TryAddEncounter(DateTime time)
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
                return true;
            }
            return false;
        }
        private void AddCombatToEncounter(Combat combat, bool isRealtime)
        {
            if (CurrentEncounter == null)
                return;
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
            CombatSelectionMonitor.DeselectCombat(unslectedCombat.Combat);
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

            //Run these in a task so that the UI can update first
            Task.Run(() =>
            {
                CombatSelectionMonitor.SelectCompleteCombat(selectedCombat.Combat);
                CombatSelectionMonitor.CheckForLeaderboardOnSelectedCombat(selectedCombat.Combat);
            });


        }
        private void NewCombatStatusAlert(CombatStatusUpdate update)
        {
            Logging.LogInfo("Received combat state change notification: " + update.Type + " at " + update.CombatStartTime + " with location " + update.CombatLocation);
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
