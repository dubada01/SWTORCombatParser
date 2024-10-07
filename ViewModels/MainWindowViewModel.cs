﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.DataStructures.Phases;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Notes;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.Death_Review;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Battle_Review;
using SWTORCombatParser.Views.DataGrid_Views;
using SWTORCombatParser.Views.HistoricalLogs;
using SWTORCombatParser.Views.Home_Views;
using SWTORCombatParser.Views.Home_Views.PastCombatViews;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overviews;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.Views.SettingsView;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MsBox.Avalonia;
using ReactiveUI;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Timers;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel :ReactiveObject
    {
        private readonly PlotViewModel _plotViewModel;
        private readonly BattleReviewViewModel _reviewViewModel;
        private readonly CombatMonitorViewModel _combatMonitorViewModel;
        private readonly OverlayViewModel _overlayViewModel;
        private readonly OverviewViewModel _tableViewModel;
        private readonly DataGridViewModel _dataGridViewModel;
        private readonly DeathReviewViewModel _deathViewModel;
        //private readonly LeaderboardViewModel _leaderboardViewModel;
        private readonly PhaseBarViewModel _phaseBarViewModel;
        private Entity localEntity;
        private string parselyLink;
        private bool canOpenParsely;
        private SolidColorBrush uploadButtonBackground = new SolidColorBrush(Colors.WhiteSmoke);

        private readonly Dictionary<Guid, HistoricalCombatViewModel> _activeHistoricalCombatOverviews = new Dictionary<Guid, HistoricalCombatViewModel>();
        private int selectedTabIndex;
        
        public string Title { get; set; }
        public ObservableCollection<TabInstance> ContentTabs { get; set; } = new ObservableCollection<TabInstance>();
        public PastCombatsView PastCombatsView { get; set; }

        public PhaseBar PhasesBar { get; set; }
        public Combat CurrentlyDisplayedCombat { get; set; }
        public Combat UnfilteredDisplayedCombat { get; set; }
        private bool _allViewsUpToDate;
        private int activeRowSpan;
        private TabInstance _selectedTab;
        private bool _logLoaded;
        private bool _viewingLogs;

        public TabInstance SelectedTab
        {
            get => _selectedTab;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTab, value);
                foreach (var tabInstance in ContentTabs)
                {
                    tabInstance.Unselect();
                }
                _selectedTab.Select();
            }
        }

        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref selectedTabIndex, value);
                UpdateDataForNewTab();
                SelectedTab = ContentTabs[value];
            }
            
        }

        public int ActiveRowSpan
        {
            get => activeRowSpan;
            set => this.RaiseAndSetIfChanged(ref activeRowSpan, value);
        
        }

        public MainWindowViewModel(HotkeyHandler hotkeyHandler)
        {
            HotkeyHandler = hotkeyHandler;
            Leaderboards.Init();

            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version}";

            DefaultPhaseLoader.LoadBuiltinPhases();

            ClassIdentifier.InitializeAvailableClasses();
            EncounterLoader.LoadAllEncounters();
            MetricColorLoader.Init();
            MetricColorLoader.SetCurrentBrushDict();
            TimerController.Init();
            RaidNotesReader.Init();
            OrbsVariableManager.RefreshVariables();
            SwtorDetector.SwtorProcessStateChanged += ProcessChanged;

            PhaseManager.Init();
            PhaseManager.SelectedPhasesUpdated += FilterForPhase;

            MainWindowClosing.Hiding += () =>
            {
                if (!SwtorDetector.SwtorRunning)
                    _overlayViewModel!.HideOverlays();
                if (SwtorDetector.SwtorRunning && !_combatMonitorViewModel!.LiveParseActive)
                    _combatMonitorViewModel.EnableLiveParse();
                if (SwtorDetector.SwtorRunning)
                    _overlayViewModel!.OverlaysLocked = true;
            };
            _combatMonitorViewModel = new CombatMonitorViewModel();
            CombatSelectionMonitor.CombatSelected += SelectCombat;
            Observable.FromEvent<double>(
                manager => _combatMonitorViewModel.OnNewLogTimeOffsetMs += manager,
                manager => _combatMonitorViewModel.OnNewLogTimeOffsetMs -= manager).Buffer(TimeSpan.FromSeconds(2)).Subscribe(UpdateLogTimeOffset);
            Observable.FromEvent<double>(
    manager => _combatMonitorViewModel.OnNewTotalTimeOffsetMs += manager,
    manager => _combatMonitorViewModel.OnNewTotalTimeOffsetMs -= manager).Buffer(TimeSpan.FromSeconds(2)).Subscribe(UpdateTotalTimeOffset);
            Observable.FromEvent<Combat>(
                manager => _combatMonitorViewModel.OnLiveCombatUpdate += manager,
                manager => _combatMonitorViewModel.OnLiveCombatUpdate -= manager).Sample(TimeSpan.FromSeconds(2)).Subscribe(UpdateCombat);
            _combatMonitorViewModel.OnMonitoringStateChanged += MonitoringStarted;
            _combatMonitorViewModel.LocalPlayerId += LocalPlayerChanged;

            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);

            _dataGridViewModel = new DataGridViewModel();
            var dataGridView = new DataGridView(_dataGridViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = dataGridView, HeaderText = "Raid Data", TabIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/grid.png") });

            _plotViewModel = new PlotViewModel();
            var graphView = new GraphView(_plotViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = graphView, HeaderText = "Plot", TabIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/chart.png") });


            _tableViewModel = new TableViewModel();
            var tableView = new OverviewView(_tableViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = tableView, HeaderText = "Details" , TabIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/bar-graph.png") });

            _overlayViewModel = new OverlayViewModel();
            _overlayViewModel.OverlayLockStateChanged += () => this.RaisePropertyChanged(nameof(OverlayLockIcon));
            // var overlayView = new OverlayView(_overlayViewModel);
            // var overlayTab = new TabInstance()
            // { TabContent = overlayView, HeaderText = "Overlays", IsOverlaysTab = true };
            // _overlayViewModel.OverlayLockStateChanged += overlayTab.UpdateLockIcon;

            _deathViewModel = new DeathReviewViewModel();
            var deathView = new DeathReviewPage(_deathViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = deathView, HeaderText = "Death Review", TabIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/skull.png") });

             _reviewViewModel = new BattleReviewViewModel();
            // ContentTabs.Add(new TabInstance() { TabContent = new BattleReviewView(_reviewViewModel), HeaderText = "Combat Log", TabIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/google-docs.png") });


            //ContentTabs.Add(overlayTab);

            //_leaderboardViewModel = new LeaderboardViewModel();
            //var leaderboardView = new LeaderboardView(_leaderboardViewModel);
            //ContentTabs.Add(new TabInstance { TabContent = leaderboardView, HeaderText = "Leaderboards" });

            _phaseBarViewModel = new PhaseBarViewModel();
            PhasesBar = new PhaseBar(_phaseBarViewModel);

            SelectedTabIndex = 0;
            ParselyUploader.UploadCompleted += HandleParselyUploadComplete;
            ParselyUploader.UploadStarted += HandleParselyUploadStart;

        }
        private void FilterForPhase(List<PhaseInstance> list)
        {
            if (UnfilteredDisplayedCombat == null || CurrentlyDisplayedCombat == null)
                return;
            if (list.Count == 0)
            {
                if (CurrentlyDisplayedCombat.DurationMS != UnfilteredDisplayedCombat.DurationMS)
                {
                    CombatSelectionMonitor.SelectPhase(UnfilteredDisplayedCombat);
                    UpdateViewsWithSelectedCombat(UnfilteredDisplayedCombat);
                }
                return;
            }
            list.ForEach(p => p.PhaseEnd = p.PhaseEnd == DateTime.MinValue ? UnfilteredDisplayedCombat.EndTime : p.PhaseEnd);
            var logsDuringPhases = UnfilteredDisplayedCombat.AllLogs.Where(l => list.Any(p => p.ContainsTime(l.TimeStamp))).ToList();
            var newCombat = CombatIdentifier.GenerateNewCombatFromLogs(logsDuringPhases);
            CombatSelectionMonitor.SelectPhase(newCombat);
            UpdateViewsWithSelectedCombat(newCombat);
        }
        public SolidColorBrush UploadButtonBackground
        {
            get => uploadButtonBackground; set => this.RaiseAndSetIfChanged(ref uploadButtonBackground, value);
        }
        public ReactiveCommand<Unit,Unit> OpenSettingsWindowCommand => ReactiveCommand.Create(OpenSettingsWindow);

        private void OpenSettingsWindow()
        {
            HotkeyHandler.UnregAll();
            var settingsWindow = new SettingsWindow();
            //settingsWindow.Owner = App.Current.MainWindow;
            settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            settingsWindow.Closing += (e,s) => 
            {
                HotkeyHandler.UpdateKeys();
            };
            // Check if we're using the ClassicDesktop style and retrieve the handle
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    settingsWindow.ShowDialog(desktop.MainWindow);
                });
            }
        }

        public bool LogLoaded
        {
            get => _logLoaded;
            set => this.RaiseAndSetIfChanged(ref _logLoaded, value);
        }

        public ReactiveCommand<Unit,Unit> OpenLogViewWindow => ReactiveCommand.Create(OpenLogView);
        
        private void OpenLogView()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var logView = new BattleReviewView(_reviewViewModel);
                logView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                logView.Closing += (e, s) =>
                {
                    _viewingLogs = false;
                };
                logView.Show(desktop.MainWindow);
                _viewingLogs = true;
            }
        }
        public ReactiveCommand<Unit,Unit> OpenOverlaySettingsCommand => ReactiveCommand.Create(OpenOverlaySettings);

        private void OpenOverlaySettings()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var overlaySettingsView = new OverlayView(_overlayViewModel);
                overlaySettingsView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                overlaySettingsView.Show(desktop.MainWindow);
            }
        }
        public ReactiveCommand<Unit, Unit> ToggleOverlayLockCommand => ReactiveCommand.Create(ToggleOverlayLock);

        private void ToggleOverlayLock()
        {
            _overlayViewModel.OverlaysLocked = !_overlayViewModel.OverlaysLocked;
        }
        public ReactiveCommand<Unit,Unit> ShowTimerWindowCommand => ReactiveCommand.Create(ShowTimerWindow);

        private void ShowTimerWindow()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var timersView = new TimersCreationView();
                _overlayViewModel._timersViewModel.RefreshEncounterSelection();
                timersView.DataContext = _overlayViewModel._timersViewModel;
                timersView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                timersView.Show(desktop.MainWindow);
            }
        }
        public ReactiveCommand<Unit,Unit> ShowChallengeWindowCommand => ReactiveCommand.Create(ShowChallengeWindow);

        private void ShowChallengeWindow()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var challengeView = new ChallengeSetupView();
                _overlayViewModel._challengesViewModel.RefreshEncounterSelection();
                challengeView.DataContext = _overlayViewModel._challengesViewModel;
                challengeView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                challengeView.Show(desktop.MainWindow);
            }
        }
        public Bitmap OverlayLockIcon => _overlayViewModel.OverlaysLocked
            ? ImageHelper.LoadFromResource("avares://Orbs/resources/lockedIcon.png")
            : ImageHelper.LoadFromResource("avares://Orbs/resources/unlockedIcon.png");
        public ReactiveCommand<Unit, Task> OpenPastCombatsCommand => _combatMonitorViewModel.LoadSpecificLogCommand;
        public ReactiveCommand<Unit,Unit> OpenParselyCommand => ReactiveCommand.Create(OpenParsely);


        public bool CanOpenParsely
        {
            get => canOpenParsely; set => this.RaiseAndSetIfChanged(ref canOpenParsely, value);
        }
        private void OpenParsely()
        {
            Process.Start(new ProcessStartInfo(parselyLink) { UseShellExecute = true });
        }
        public ReactiveCommand<Unit,Unit> OpenParselyConfigCommand => ReactiveCommand.Create(OpenParselyConfig);

        private void OpenParselyConfig()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var parselySettingsWindow = new ParselySettings();
                parselySettingsWindow.ShowDialog(desktop.MainWindow);
            }
        }

        public ReactiveCommand<Unit,Unit> UploadToParselyCommand => ReactiveCommand.Create(UploadToParsely);

        public HotkeyHandler HotkeyHandler { get; internal set; }

        private void HandleParselyUploadComplete(bool status, string link)
        {
            if (status)
            {
                UploadButtonBackground = new SolidColorBrush(Colors.MediumSeaGreen);
                parselyLink = link;
                CanOpenParsely = true;
            }
            else
            {
                UploadButtonBackground = new SolidColorBrush(Colors.Salmon);
                CanOpenParsely = false;
            }
            Task.Run(() =>
            {
                Thread.Sleep(2000);
                Dispatcher.UIThread.Invoke(() =>
                {
                    UploadButtonBackground = new SolidColorBrush(Colors.WhiteSmoke);
                });
            });
        }
        private void HandleParselyUploadStart()
        {
            UploadButtonBackground = new SolidColorBrush(Colors.CornflowerBlue);
        }
        private async void UploadToParsely()
        {
            var response = await ParselyUploader.UploadCurrentCombat(_combatMonitorViewModel.GetActiveFile());
            if (!string.IsNullOrEmpty(response))
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Error", response);
                await box.ShowAsync();
            }
        }

        private void UpdateDataForNewTab()
        {
            if (CurrentlyDisplayedCombat != null && _allViewsUpToDate == false)
                SelectCombat(CurrentlyDisplayedCombat);
        }

        private void ProcessChanged(bool obj)
        {
            if (obj)
            {
                if (LoadingWindowFactory.MainWindowHidden)
                {
                    _combatMonitorViewModel.EnableLiveParse(true);
                    _overlayViewModel.OverlaysLocked = true;
                }
            }
            else
            {
                _combatMonitorViewModel.DisableLiveParse();
                if (LoadingWindowFactory.MainWindowHidden)
                    _overlayViewModel.HideOverlays();
            }
        }
        private void MonitoringStarted(bool state)
        {
            if (state)
                Dispatcher.UIThread.Invoke(delegate
                {
                    _plotViewModel.Reset();
                    _tableViewModel.Reset();
                    _deathViewModel.Reset();
                    _dataGridViewModel.Reset();
                    _reviewViewModel.Reset();
                });
        }

        private void UpdateLogTimeOffset(IList<double> logOffsetFor2Seconds)
        {
            if (!logOffsetFor2Seconds.Any())
                return;
            var average = logOffsetFor2Seconds.Average() / 1000d;
            _combatMonitorViewModel.CurrentLogOffsetMs = Math.Round(average, 1);
        }
        private void UpdateTotalTimeOffset(IList<double> logOffsetFor2Seconds)
        {
            if (!logOffsetFor2Seconds.Any())
                return;
            var average = logOffsetFor2Seconds.Average() / 1000d;
            _combatMonitorViewModel.CurrentTotalOffsetMs = Math.Round(average, 1);
        }
        private void UpdateCombat(Combat updatedCombat)
        {
            CurrentlyDisplayedCombat = updatedCombat;
            if (LoadingWindowFactory.MainWindowHidden)
                return;
            Dispatcher.UIThread.Invoke(delegate
            {
                _overlayViewModel.CombatUpdated(updatedCombat);
                switch (SelectedTabIndex)
                {
                    case 1:
                        _plotViewModel.UpdateParticipants(updatedCombat);
                        _plotViewModel.UpdateLivePlot(updatedCombat);
                        break;
                    case 2:
                        _tableViewModel.AddCombat(updatedCombat);
                        break;
                    case 0:
                        _dataGridViewModel.UpdateCombat(updatedCombat);
                        break;
                }
                if(_viewingLogs)
                    _reviewViewModel.CombatSelected(updatedCombat);
            });

            _allViewsUpToDate = false;
        }
        private void SelectCombat(Combat selectedCombat)
        {
            LogLoaded = true;
            UnfilteredDisplayedCombat = selectedCombat;
            UpdateViewsWithSelectedCombat(selectedCombat);
        }
        private void UpdateViewsWithSelectedCombat(Combat selectedCombat)
        {

            Dispatcher.UIThread.Invoke(delegate
            {
                CurrentlyDisplayedCombat = selectedCombat;
                _overlayViewModel.CombatSeleted(selectedCombat);
                _plotViewModel.UpdateParticipants(selectedCombat);
                _plotViewModel.AddCombatPlot(selectedCombat);
                _tableViewModel.AddCombat(selectedCombat);
                _deathViewModel.AddCombat(selectedCombat);
                _reviewViewModel.CombatSelected(selectedCombat);
                _dataGridViewModel.UpdateCombat(selectedCombat);

                _allViewsUpToDate = true;
            });
        }

        private void LocalPlayerChanged(Entity obj)
        {
            if (localEntity == obj)
                return;
            Dispatcher.UIThread.Invoke(delegate
            {
                if (localEntity != obj)
                {
                    _plotViewModel.Reset();
                    _tableViewModel.Reset();
                    _deathViewModel.Reset();
                    _dataGridViewModel.Reset();
                }
                localEntity = obj;
            });
        }
    }
}
