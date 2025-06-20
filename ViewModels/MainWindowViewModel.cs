using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
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
using SWTORCombatParser.Views.Home_Views;
using SWTORCombatParser.Views.Home_Views.PastCombatViews;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overviews;
using SWTORCombatParser.Views.Phases;
using SWTORCombatParser.Views.SettingsView;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
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
using SWTORCombatParser.DataStructures.Phases;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Death_Review;
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
        private readonly RaidwideBattleReviewViewModel _deathViewModel;
        //private readonly LeaderboardViewModel _leaderboardViewModel;
        private readonly PhaseBarViewModel _phaseBarViewModel;
        private Entity localEntity;
        private string parselyLink = "https://parsely.io/";
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
        private readonly BattleReviewView _logView;
        private readonly TimersCreationView _timersView;
        private readonly ChallengeSetupView _challengeView;
        private readonly RaidwideBattleReviewWindow _deathView;

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
                switch (SelectedTabIndex)
                {
                    case 0:
                        Settings.WriteSetting("current_tab","data_grid");
                        break;
                    case 1:
                        Settings.WriteSetting("current_tab","plot");
                        break;
                    case 2:
                        Settings.WriteSetting("current_tab","details");
                        break;
                    case 3:
                        Settings.WriteSetting("current_tab","log");
                        break;
                    default:
                        break;
                }
                
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
            TimerController.TimersInitialized += OrbsVariableManager.RefreshVariables;
            TimerController.Init();
            RaidNotesReader.Init();
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

            _reviewViewModel = new BattleReviewViewModel();
            _logView = new BattleReviewView(_reviewViewModel);
            ContentTabs.Add(new TabInstance()
            {
                TabContent = _logView, HeaderText = "Log Review",
                TabIcon = ImageHelper.LoadFromResource("avares://Orbs/resources/google-docs.png")
            });
            
            
            _overlayViewModel = new OverlayViewModel();
            _overlayViewModel.OverlayLockStateChanged += () => this.RaisePropertyChanged(nameof(OverlayLockIcon));
            _timersView = new TimersCreationView();
            _timersView.DataContext = _overlayViewModel._timersViewModel;
            _timersView.Closing += (e, s) =>
            {
                s.Cancel = true;
                _timersView.Hide();
            };
            _challengeView = new ChallengeSetupView();
            _challengeView.DataContext = _overlayViewModel._challengesViewModel;
            _challengeView.Closing += (e, s) =>
            {
                s.Cancel = true;
                _challengeView.Hide();
            };
            _deathViewModel = new RaidwideBattleReviewViewModel();
            _deathView = new RaidwideBattleReviewWindow(_deathViewModel);
            _deathView.Closing += (e, s) =>
            {
                s.Cancel = true;
                _deathView.Hide();
            };
             
            _phaseBarViewModel = new PhaseBarViewModel();
            PhasesBar = new PhaseBar(_phaseBarViewModel);
            var selectedTab = Settings.ReadSettingOfType<string>("current_tab");
            SelectedTabIndex = selectedTab switch
            {
                "data-grid" => 0,
                "details" => 2,
                "plot" => 1,
                "log" => 3,
                _ => SelectedTabIndex
            };
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
            var phaseList = new ConcurrentDictionary<Guid, PhaseInstance>(list.ToDictionary(_=>  Guid.NewGuid(), kvp=> kvp));
            var phaseCombat = UnfilteredDisplayedCombat.GetPhaseCopy(phaseList);
            CombatSelectionMonitor.SelectPhase(phaseCombat);
            UpdateViewsWithSelectedCombat(phaseCombat);
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
                _overlayViewModel._timersViewModel.RefreshEncounterSelection();
                _timersView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _timersView.Show(desktop.MainWindow);
            }
        }
        public void ShowDeathReviewForCombat(Combat viewModelCombat)
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _deathViewModel.Reset();
                _deathViewModel.SetCombat(viewModelCombat);
                _deathView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _deathView.Show(desktop.MainWindow);
            }
        }
        public ReactiveCommand<Unit,Unit> ShowChallengeWindowCommand => ReactiveCommand.Create(ShowChallengeWindow);

        private void ShowChallengeWindow()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _overlayViewModel._challengesViewModel.RefreshEncounterSelection();
                _challengeView.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _challengeView.Show(desktop.MainWindow);
            }
        }
        public Bitmap OverlayLockIcon => _overlayViewModel.OverlaysLocked
            ? ImageHelper.LoadFromResource("avares://Orbs/resources/lockedIcon.png")
            : ImageHelper.LoadFromResource("avares://Orbs/resources/unlockedIcon.png");
        public ReactiveCommand<Unit, Task> OpenPastCombatsCommand => _combatMonitorViewModel.LoadSpecificLogCommand;
        public ReactiveCommand<Unit,Unit> OpenParselyCommand => ReactiveCommand.Create(OpenParsely);

        public ReactiveCommand<Unit, Unit> OpenOrbsStatsCommand => ReactiveCommand.Create(OpenOrbsStats);
            
        private void OpenOrbsStats()
        {
            Process.Start(new ProcessStartInfo
                { FileName = "https://orbs-stats.com", UseShellExecute = true });
        }
        public ReactiveCommand<Unit, Unit> OpenBuyMeACoffeeCommand => ReactiveCommand.Create(OpenBuyMeACoffee);
            
        private void OpenBuyMeACoffee()
        {
            Process.Start(new ProcessStartInfo
                { FileName = "https://buymeacoffee.com/dubatech", UseShellExecute = true });
        }

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
                parselySettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                parselySettingsWindow.ShowDialog(desktop.MainWindow);
            }
        }
        public ReactiveCommand<Unit,Unit> OpenPhaseConfigCommand => _phaseBarViewModel.ConfigurePhasesCommand;
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
                parselyLink = "https://parsely.io/";
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
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Error", response);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }
            }
            else
            {
                if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var box = MessageBoxManager.GetMessageBoxStandard("Success",
                        "Successfully uploaded to Parsely! Use the File->Parsely menu in Orbs to open and view your battle.",
                        windowStartupLocation: WindowStartupLocation.CenterOwner);
                    await box.ShowWindowDialogAsync(desktop.MainWindow);
                }
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
            try
            {
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
            catch (Exception e)
            {
                Logging.LogError("Failed to update combat visuals: " + e.Message + "\r\n" + e.StackTrace);
            }
        }
        private void SelectCombat(Combat selectedCombat)
        {
            LogLoaded = true;
            UnfilteredDisplayedCombat = selectedCombat;
            UpdateViewsWithSelectedCombat(selectedCombat);
        }
        private void UpdateViewsWithSelectedCombat(Combat selectedCombat)
        {
            try
            {
                Dispatcher.UIThread.Invoke(delegate
                {
                    CurrentlyDisplayedCombat = selectedCombat;
                    _overlayViewModel.CombatSeleted(selectedCombat);
                    _plotViewModel.UpdateParticipants(selectedCombat);
                    _plotViewModel.AddCombatPlot(selectedCombat);
                    _tableViewModel.AddCombat(selectedCombat);
                    if(_deathView.IsVisible) 
                        _deathViewModel.SetCombat(selectedCombat);
                    _reviewViewModel.CombatSelected(selectedCombat);
                    _dataGridViewModel.UpdateCombat(selectedCombat);

                    _allViewsUpToDate = true;
                });
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to update combat visuals: " + e.Message + "\r\n" + e.StackTrace);
            }
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
