using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatCaching;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MsBox.Avalonia;
using ReactiveUI;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public partial class CombatMonitorViewModel :ReactiveObject, INotifyPropertyChanged
    {
        private ConcurrentDictionary<DateTime, List<ParsedLogEntry>> _totalLogsDuringCombat = new ConcurrentDictionary<DateTime, List<ParsedLogEntry>>();
        private static bool _liveParseActive;
        private static bool _autoParseEnabled;
        private CombatLogStreamer _combatLogStreamer;
        private int _numberOfSelectedCombats = 0;
        private bool showTrash;
        private List<EncounterCombat> _allEncounters = new List<EncounterCombat>();
        private bool _usingHistoricalData = true;
        private HistoricalRangeSelectionViewModel _historicalRangeVM;
        private double currentLogOffsetMs;
        private double currentTotalOffsetMs;
        private PastCombat? _loadedHistoricalCombat;
        private HistoricalCombatSelection? _historicalSelection;
        private HistoricalCombatSelection? _displayedHistoricalSelection;
        private DateTime _currentEncounterVisit;
        private int _monitorRequestGeneration;
        private int _combatSelectionGeneration;

        public event Action<bool> OnMonitoringStateChanged = delegate { };
        public event Action<List<Combat>> OnHistoricalCombatsParsed = delegate { };
        public event Action<Combat> OnLiveCombatUpdate = delegate { };
        internal event Action<Combat> HistoricalCombatDataReady = delegate { };
        internal event Action<PastCombat> CompletedLivePull = delegate { };
        public event Action<double> OnNewLogTimeOffsetMs = delegate { };
        public event Action<double> OnNewTotalTimeOffsetMs = delegate { };
        public event Action<string> OnNewLog = delegate { };
        public event Action<Entity> LocalPlayerId = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public string CurrentlySelectedLogName { get; set; }
        public HistoricalCombatSelection? HistoricalSelection
        {
            get => _historicalSelection;
            private set
            {
                _historicalSelection = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasHistoricalSelection));
                OnPropertyChanged(nameof(CanShowCombatPhases));
            }
        }
        public bool HasHistoricalSelection => HistoricalSelection != null;
        public bool CanShowCombatPhases => HistoricalSelection is null or { Stage: HistoricalCombatSelectionStage.Ready, HasError: false };
        internal bool IsReviewingHistoricalCombat => HistoricalSelection?.Row.IsSelected == true;
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
        public ReactiveCommand<Unit,Unit> AutoLiveParseCommand => ReactiveCommand.Create(AutoLiveParseToggle);

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
            _autoParseEnabled = Settings.ReadSettingOfType<bool>("Auto_Parse");
            
            _combatLogStreamer = new CombatLogStreamer();
            _combatLogStreamer.NewLogTimeOffsetMs += UpdateLogOffset;
            _combatLogStreamer.NewTotalTimeOffsetMs += UpdateTotalOffset;
            _combatLogStreamer.LocalPlayerIdentified += LocalPlayerFound;
            _combatLogStreamer.ErrorParsingLogs += LiveParseError;
            _combatLogStreamer.LogFileRotated += Reset;
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
            Dispatcher.UIThread.Invoke(() =>
            {
                UnSelectAll();
                ClearHistoricalCombatDisplay();
                CombatDiskCacheManager.ClearRecentCombats();
                _usingHistoricalData = true;
                PastEncounters.Clear();
                CurrentEncounter = null;
                _totalLogsDuringCombat.Clear();
                CombatIdentifier.CurrentCombat = new Combat();
                CombatLogStateBuilder.ClearState();
                ClearCombats();
            });
        }
        private ReactiveCommand<Unit, Unit>? _toggleLiveParseCommand;
        public ReactiveCommand<Unit,Unit> ToggleLiveParseCommand => _toggleLiveParseCommand ??= ReactiveCommand.CreateFromTask(ToggleLiveParse);

        private async Task ToggleLiveParse()
        {
            if (!LiveParseActive)
            {
                EnableLiveParse();
            }
            else
            {
                await DisableLiveParse();
            }

        }
        public void EnableLiveParse(bool runningInBackground = false)
        {
            if (IsReplaying) return;
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
            var requestGeneration = Interlocked.Increment(ref _monitorRequestGeneration);

            Task.Run(() =>
            {
                while (!CombatLogLoader.CheckIfCombatLogsArePresent() && LiveParseActive && !IsReplaying &&
                       requestGeneration == Volatile.Read(ref _monitorRequestGeneration))
                {
                    Thread.Sleep(100);
                }
                if (!LiveParseActive || IsReplaying || requestGeneration != Volatile.Read(ref _monitorRequestGeneration))
                    return;
                else
                {
                    MonitorMostRecentLog(runningInBackground, requestGeneration);
                }
            });
        }

        private void MonitorMostRecentLog(bool runningInBackground, int requestGeneration)
        {
            if (IsReplaying || requestGeneration != Volatile.Read(ref _monitorRequestGeneration)) return;
            ReplayStatus = "";
            if (!runningInBackground)
                LoadingWindowFactory.ShowLoading();
            OnMonitoringStateChanged(true);
            var mostRecentLog = CombatLogLoader.GetMostRecentLogPath();
            _combatLogStreamer.MonitorLog(mostRecentLog);
            OnNewLog("Started Monitoring: " + mostRecentLog);
        }

        public async void LiveParseError(string errorMessage)
        {
            if (IsReplaying) CancelReplay();
            try
            {
                await Dispatcher.UIThread.Invoke(async () =>
                {
                    var box =MessageBoxManager.GetMessageBoxStandard("Error",
                        "There was an unexpected error while parsing the combat log. Please message Zarnuro on Discord with the following error message for support if this issue persists.\r\n\r\n" + errorMessage);
                    await box.ShowAsync();
                });
            }
            catch (Exception e)
            {
                // ignored
            }
        }
        public async Task DisableLiveParse()
        {
            Interlocked.Increment(ref _monitorRequestGeneration);
            if (IsReplaying) { await StopReplayAsync(); return; }
            if (!LiveParseActive)
                return;
            LiveParseActive = false;
            await _combatLogStreamer.StopMonitoring();
            OnMonitoringStateChanged(false);

            OnNewLog("Stopped Monitoring");
        }
        public void ClearCombats()
        {
            _numberOfSelectedCombats = 0;
            _allEncounters.Clear();
        }


        private ReactiveCommand<Unit, Unit>? _loadSpecificLogCommand;
        public ReactiveCommand<Unit, Unit> LoadSpecificLogCommand =>
            _loadSpecificLogCommand ??= ReactiveCommand.CreateFromTask(LoadSpecificLog);
        private async Task LoadSpecificLog()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
                desktop.MainWindow == null)
                return;
            try
            {
                CombatLogLoader.RefreshSWTORCombatLogsDirectory();
                var browser = new Views.LogBrowser.LogBrowserWindow(CombatLogLoader.GetLogDirectory());
                var selection = await browser.ShowDialog<SWTORCombatParser.ViewModels.LogBrowser.LogBrowserSelection?>(desktop.MainWindow);
                if (selection == null)
                    return;
                var path = selection.FilePath;
                if (!File.Exists(path))
                    throw new FileNotFoundException("The selected log is no longer available. Refresh the Log Library.", path);

                if (selection.Replay != null)
                {
                    await StartReplayAsync(selection.Replay);
                    return;
                }

                // Browsing leaves the live parser running; switch only after choosing a file.
                await StopReplayAsync();
                ReplayStatus = "";
                Interlocked.Increment(ref _monitorRequestGeneration);
                await _combatLogStreamer.StopMonitoring();
                LiveParseActive = false;
                OnMonitoringStateChanged(false);
                CurrentlySelectedLogName = path;
                OnPropertyChanged(nameof(CurrentlySelectedLogName));
                Reset();
                LoadingWindowFactory.ShowLoading();
                _combatLogStreamer.ParseCompleteLog(path);
            }
            catch (Exception ex)
            {
                Logging.LogError("Could not open previous log: " + ex.Message);
                await MessageBoxManager.GetMessageBoxStandard("Unable to open log", ex.Message).ShowAsync();
            }
        }
        private void UpdateVisibleEncounters()
        {
            Dispatcher.UIThread.Invoke(delegate
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
            CombatIdentifier.ResetCombat();
            Logging.LogInfo("NEW real time combat started at " + startTime.ToString());
            AddOngoingCombat(location);
            UpdateVisibleEncounters();
            _totalLogsDuringCombat[startTime] = new List<ParsedLogEntry>();
        }

        private void CombatUpdated(List<ParsedLogEntry> obj, DateTime combatStartTime)
        {
            _totalLogsDuringCombat[combatStartTime] = obj;
            _usingHistoricalData = false;
            var combatInfo = CombatIdentifier.GenerateCombatFromLogs(_totalLogsDuringCombat[combatStartTime].ToList(), isRealtime:true);
            //only process combats if they were property created
            if(combatInfo.StartTime == DateTime.MinValue)
            {
                return;
            }
            CombatSelectionMonitor.InProgressCombatSeleted(combatInfo);
            if (CurrentEncounter == null)
                return;
            var combatUI = CurrentEncounter.UpdateOngoing(combatInfo);
            if (combatUI.IsSelected)
            {
                OnLiveCombatUpdate(combatInfo);
            }
            Leaderboards.UpdateOverlaysWithNewLeaderboard(combatInfo, false);
        }


        private void CombatStopped(List<ParsedLogEntry> obj, DateTime combatStartTime)
        {
            if(_usingHistoricalData)
                _totalLogsDuringCombat[combatStartTime] = obj;

            if (!_usingHistoricalData)
            {
                Logging.LogInfo("Real time combat started at " + combatStartTime.ToString() + " has STOPPED");
                CurrentEncounter?.RemoveOngoing();
                var combatInfo = CombatIdentifier.GenerateCombatFromLogs(obj, isRealtime:true, combatEndUpdate: true);
                //only process combats if they were property created
                if(combatInfo.StartTime == DateTime.MinValue)
                {
                    return;
                }
                //if (combatInfo.IsCombatWithBoss)
                //    Leaderboards.StartGetPlayerLeaderboardStandings(combatInfo);
                // Completing the live pull must not replace a selected historical review.
                var selectCompleted = Dispatcher.UIThread.Invoke(() =>
                {
                    if (IsReviewingHistoricalCombat) return false;
                    ClearHistoricalCombatDisplay();
                    CombatSelectionMonitor.SelectCompleteCombat(combatInfo);
                    return true;
                });
                if (_totalLogsDuringCombat.ContainsKey(combatStartTime))
                {
                    _totalLogsDuringCombat.TryRemove(combatStartTime, out var t);
                }
                AddCombatToEncounter(combatInfo, true, selectCompleted: selectCompleted);
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
            ConcurrentDictionary<DateTime, Combat> processedCombats = new ConcurrentDictionary<DateTime, Combat>();
            using (LogLoadDiagnostics.Measure("Build all historical combats"))
            {
                Parallel.ForEach(_totalLogsDuringCombat.Keys, combatStartTime =>
                {

                    List<ParsedLogEntry> combatLogs = new List<ParsedLogEntry>();
                    _totalLogsDuringCombat.TryGetValue(combatStartTime, out combatLogs);
                    if (combatLogs.Count == 0)
                        return;
                    Logging.LogInfo("Processing combat with start time " + combatStartTime + " and " +
                                    combatLogs.Count + " log entries");
                    var combatInfo =
                        CombatIdentifier.GenerateCombatSnapshotFromLogs(combatLogs, combatEndUpdate: true);
                    //only process combats if they were property created
                    if (combatInfo.StartTime == DateTime.MinValue)
                    {
                        return;
                    }

                    processedCombats[combatStartTime] = combatInfo;
                    Logging.LogInfo("Combat processed!");
                });
            }
            using (LogLoadDiagnostics.Measure("Cache combats and populate encounters"))
            {
                var orderedStartTimes = processedCombats.Keys.OrderBy(t => t).ToArray();
                foreach (var startTime in orderedStartTimes)
                {
                    var addedNewEncounter = TryAddEncounter(startTime);
                    Logging.LogInfo(addedNewEncounter ? "Added new encounter!" : "Adding to existing encounter");
                    // This combat is selected immediately after loading. Keep just it
                    // resident to avoid a disk read and a second metadata build.
                    AddCombatToEncounter(processedCombats[startTime], false, keepLoaded: startTime == orderedStartTimes[^1]);
                    Logging.LogInfo("Combat added to encounter");
                }
            }
            using (LogLoadDiagnostics.Measure("Historical combat subscribers"))
                OnHistoricalCombatsParsed.InvokeSafely(processedCombats.Values.OrderBy(c => c.StartTime).ToList());
            processedCombats.Clear();
            _totalLogsDuringCombat.Clear();
        }

        private void HistoricalLogsFinished(DateTime combatEndTime, bool localPlayerIdentified)
        {
            Logging.LogInfo("Processing logs into combats...");
            Logging.LogInfo("Detected " + _totalLogsDuringCombat.Keys.Count() + " distinct combats");
            GenerateHistoricalCombats();
            LoadingWindowFactory.HideLoading();
            _numberOfSelectedCombats = 0;
            _usingHistoricalData = false;
            using (LogLoadDiagnostics.Measure("Publish visible encounters"))
                UpdateVisibleEncounters();
            if (_allEncounters.Any())
            {
                _allEncounters.Last().EncounterCombats.First().AdditiveSelectionToggle();
                EncounterMonitor.FireEncounterUpdated();
            }
        }
        private bool TryAddEncounter(DateTime time)
        {
            var currentActiveEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(time);
            var visitStart = EncounterSessionGrouping.VisitStart(CombatLogStateBuilder.CurrentState.EncounterEnteredInfo, time);
            if (EncounterSessionGrouping.StartsNewSession(CurrentEncounter?.Info, currentActiveEncounter, _currentEncounterVisit, visitStart))
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
                _currentEncounterVisit = visitStart;
                EncounterMonitor.SetCurrentEncounter(CurrentEncounter);
                return true;
            }
            return false;
        }
        private void AddCombatToEncounter(Combat combat, bool isRealtime, bool keepLoaded = false, bool selectCompleted = true)
        {
            if (CurrentEncounter == null)
                return;
            var pull = CurrentEncounter.AddCombat(combat, isRealtime, keepLoaded, selectCompleted);
            if (isRealtime && pull.IsBossCombat && !pull.IsReplay) CompletedLivePull.InvokeSafely(pull);
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
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(UnSelectAll);
                return;
            }
            Interlocked.Increment(ref _combatSelectionGeneration);
            foreach (var combat in PastEncounters.SelectMany(e => e.EncounterCombats).Where(c => c.IsSelected))
            {
                combat.IsSelected = false;
            }
        }
        private void UnselectCombat(PastCombat unslectedCombat)
        {
            _numberOfSelectedCombats--;
            _numberOfSelectedCombats = Math.Max(_numberOfSelectedCombats, 0);
            if (HistoricalSelection?.Row == unslectedCombat && HistoricalSelection.Stage != HistoricalCombatSelectionStage.Ready)
            {
                Interlocked.Increment(ref _combatSelectionGeneration);
                if (_displayedHistoricalSelection is { Stage: HistoricalCombatSelectionStage.LoadingPhases } displayed)
                    _displayedHistoricalSelection = displayed with
                    {
                        Stage = HistoricalCombatSelectionStage.Ready,
                        ShowProgress = false,
                        Error = "Reselect this combat to load its phases."
                    };
                HistoricalSelection = _displayedHistoricalSelection;
            }
            var combat = unslectedCombat.Combat;
            if (combat == null)
                return;
            OnNewLog("Removing combat: " + unslectedCombat.CombatLabel + " from plot.");
            CombatSelectionMonitor.DeselectCombat(combat);
            if (!unslectedCombat.IsCurrentCombat)
                unslectedCombat.UnloadCombat();
        }
        private async void SelectCombat(PastCombat selectedCombat)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => SelectCombat(selectedCombat));
                return;
            }
            var selectionGeneration = Interlocked.Increment(ref _combatSelectionGeneration);
            OnNewLog("Displaying new combat: " + selectedCombat.CombatLabel);
            bool IsLatest() => selectionGeneration == Volatile.Read(ref _combatSelectionGeneration);
            bool CanPublish() => IsLatest() && selectedCombat.IsSelected;
            var isHistorical = !selectedCombat.IsCurrentCombat;
            if (isHistorical)
            {
                HistoricalSelection = new(selectedCombat, HistoricalCombatSelectionStage.Opening,
                    ShowsPreviousResults: _loadedHistoricalCombat != null || LiveParseActive);
                DispatcherTimer.RunOnce(() =>
                {
                    if (CanPublish() && HistoricalSelection is { IsBusy: true } pending)
                        HistoricalSelection = pending with { ShowProgress = true };
                }, TimeSpan.FromMilliseconds(150));
            }
            else
                ClearHistoricalCombatDisplay();

            try
            {
                var (combat, prepared) = await Task.Run(() =>
                {
                    if (!IsLatest()) return ((Combat?)null, (PhaseManager.PreparedSelection?)null);
                    Combat loaded;
                    using (LogLoadDiagnostics.Measure("Load selected combat")) loaded = selectedCombat.GetCombat();
                    return (loaded, isHistorical && loaded.StartTime != DateTime.MinValue
                        ? PhaseManager.GetPreparedSelectionIfReady(loaded) : null);
                }).ConfigureAwait(false);
                if (combat == null) return;

                var needsPhases = await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!CanPublish()) return false;
                    if (combat.StartTime == DateTime.MinValue)
                    {
                        ReportCombatSelectionError(selectedCombat, selectedCombat.CombatLoadError);
                        return false;
                    }

                    if (_loadedHistoricalCombat != null && _loadedHistoricalCombat != selectedCombat)
                        _loadedHistoricalCombat.UnloadCombat();
                    if (isHistorical) _loadedHistoricalCombat = selectedCombat;
                    if (!isHistorical || prepared != null)
                    {
                        PublishSelectedCombat(selectedCombat, combat, prepared);
                        return false;
                    }

                    _displayedHistoricalSelection = HistoricalSelection! with
                    {
                        Stage = HistoricalCombatSelectionStage.LoadingPhases,
                        ShowsPreviousResults = false
                    };
                    HistoricalCombatDataReady(combat);
                    HistoricalSelection = _displayedHistoricalSelection;
                    return true;
                }, DispatcherPriority.Background);
                if (!needsPhases) return;

                // Finish the active view first. Publish the independent phase scan at
                // background dispatcher priority so input and rendering can run first.
                string? phaseError = null;
                try
                {
                    prepared = await Task.Run(() => PhaseManager.PrepareSelection(combat, IsLatest)).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    Debug.WriteLine("Could not load selected combat phases: " + error);
                    phaseError = "Phases could not be loaded. Reselect this combat to retry.";
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (CanPublish()) PublishSelectedCombat(selectedCombat, combat, prepared, phaseError);
                }, DispatcherPriority.Background);
            }
            catch (Exception error)
            {
                Debug.WriteLine("Could not display selected combat: " + error);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (CanPublish()) ReportCombatSelectionError(selectedCombat, "Could not open this combat. Reselect it to retry.");
                });
            }
            finally
            {
                if (isHistorical && !IsLatest())
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_loadedHistoricalCombat != selectedCombat &&
                            (HistoricalSelection?.Row != selectedCombat || !selectedCombat.IsSelected))
                            selectedCombat.UnloadCombat();
                    });
            }
        }

        private void PublishSelectedCombat(PastCombat row, Combat combat, PhaseManager.PreparedSelection? phases, string? phaseError = null)
        {
            EncounterMonitor.SetCurrentEncounter(row.ParentEncounter);
            CombatSelectionMonitor.SelectCombat(combat, row.IsCurrentCombat, phases);
            if (!row.IsCurrentCombat)
            {
                _displayedHistoricalSelection = new(row, HistoricalCombatSelectionStage.Ready, Error: phaseError);
                HistoricalSelection = _displayedHistoricalSelection;
            }
            CombatSelectionMonitor.CheckForLeaderboardOnSelectedCombat(combat);
        }

        private void ReportCombatSelectionError(PastCombat row, string message)
        {
            OnNewLog(message);
            if (!row.IsCurrentCombat)
            {
                HistoricalSelection = HistoricalSelection! with
                {
                    Stage = HistoricalCombatSelectionStage.Unavailable,
                    ShowProgress = false,
                    Error = message
                };
                return;
            }
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Combat data unavailable", message,
                    windowStartupLocation: WindowStartupLocation.CenterOwner);
                _ = box.ShowWindowDialogAsync(desktop.MainWindow);
            }
        }

        internal void ClearHistoricalCombatDisplay()
        {
            _loadedHistoricalCombat?.UnloadCombat();
            _loadedHistoricalCombat = null;
            _displayedHistoricalSelection = null;
            if (HistoricalSelection != null) HistoricalSelection = null;
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
