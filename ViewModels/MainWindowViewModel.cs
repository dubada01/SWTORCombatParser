using Prism.Commands;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.DataStructures.Phases;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.Death_Review;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.ViewModels.Leaderboard;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Battle_Review;
using SWTORCombatParser.Views.DataGrid_Views;
using SWTORCombatParser.Views.HistoricalLogs;
using SWTORCombatParser.Views.Home_Views;
using SWTORCombatParser.Views.Home_Views.PastCombatViews;
using SWTORCombatParser.Views.Leaderboard_View;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overviews;
using SWTORCombatParser.Views.Phases;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly PlotViewModel _plotViewModel;
        private readonly BattleReviewViewModel _reviewViewModel;
        private readonly CombatMonitorViewModel _combatMonitorViewModel;
        private readonly OverlayViewModel _overlayViewModel;
        private readonly OverviewViewModel _tableViewModel;
        private readonly DataGridViewModel _dataGridViewModel;
        private readonly DeathReviewViewModel _deathViewModel;
        private readonly LeaderboardViewModel _leaderboardViewModel;
        private readonly PhaseBarViewModel _phaseBarViewModel;
        private Entity localEntity;
        private string parselyLink;
        private bool canOpenParsely;
        private SolidColorBrush uploadButtonBackground = Brushes.WhiteSmoke;

        private readonly Dictionary<Guid, HistoricalCombatViewModel> _activeHistoricalCombatOverviews = new Dictionary<Guid, HistoricalCombatViewModel>();
        private int selectedTabIndex;

        public event PropertyChangedEventHandler PropertyChanged;
        public string Title { get; set; }
        public ObservableCollection<TabInstance> ContentTabs { get; set; } = new ObservableCollection<TabInstance>();
        public PastCombatsView PastCombatsView { get; set; }

        public PhaseBar PhasesBar { get; set; }
        public Combat CurrentlyDisplayedCombat { get; set; }
        public Combat UnfilteredDisplayedCombat { get; set; }
        private bool _allViewsUpToDate;
        private int activeRowSpan;

        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set
            {
                selectedTabIndex = value;
                OnPropertyChanged();
            }
        }
        public int ActiveRowSpan
        {
            get => activeRowSpan; set
            {
                activeRowSpan = value;
                OnPropertyChanged();
            }
        }
        public bool AppIsOutOfDate { get; set; }
        public ICommand AppOutOfDateCommand => new DelegateCommand(VersionChecker.OpenMicrosoftStoreToAppPage);

        public MainWindowViewModel()
        {
            Leaderboards.Init();

            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version}";

            DefaultPhaseLoader.LoadBuiltinPhases();

            ClassIdentifier.InitializeAvailableClasses();
            EncounterLoader.LoadAllEncounters();

            TimerController.Init();
            VariableManager.RefreshVariables();
            SwtorDetector.SwtorProcessStateChanged += ProcessChanged;

            VersionChecker.AppVersionInfoReady += UpdateIcon;

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
            _combatMonitorViewModel.OnHistoricalCombatsParsed += AddHistoricalViewer;


            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);

            _dataGridViewModel = new DataGridViewModel();
            var dataGridView = new DataGridView(_dataGridViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = dataGridView, HeaderText = "Raid Data" });

            _plotViewModel = new PlotViewModel();
            var graphView = new GraphView(_plotViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = graphView, HeaderText = "Battle Plot" });


            _tableViewModel = new TableViewModel();
            var tableView = new OverviewView(_tableViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = tableView, HeaderText = "Details" });

            _overlayViewModel = new OverlayViewModel();
            var overlayView = new OverlayView(_overlayViewModel);
            var overlayTab = new TabInstance()
            { TabContent = overlayView, HeaderText = "Overlays", IsOverlaysTab = true };
            _overlayViewModel.OverlayLockStateChanged += overlayTab.UpdateLockIcon;

            _deathViewModel = new DeathReviewViewModel();
            var deathView = new DeathReviewPage(_deathViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = deathView, HeaderText = "Death Review" });

            _reviewViewModel = new BattleReviewViewModel();
            ContentTabs.Add(new TabInstance() { TabContent = new BattleReviewView(_reviewViewModel), HeaderText = "Combat Log" });


            ContentTabs.Add(overlayTab);

            _leaderboardViewModel = new LeaderboardViewModel();
            var leaderboardView = new LeaderboardView(_leaderboardViewModel);
            ContentTabs.Add(new TabInstance { TabContent = leaderboardView, HeaderText = "Leaderboards" });

            _phaseBarViewModel = new PhaseBarViewModel();
            PhasesBar = new PhaseBar(_phaseBarViewModel);

            SelectedTabIndex = 0;
            HeaderSelectionState.NewHeaderSelected += UpdateDataForNewTab;
            ParselyUploader.UploadCompleted += HandleParselyUploadComplete;
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

        private void UpdateIcon()
        {
            AppIsOutOfDate = !VersionChecker.AppIsUpToDate;
            OnPropertyChanged("AppIsOutOfDate");
        }

        public SolidColorBrush UploadButtonBackground
        {
            get => uploadButtonBackground; set
            {
                uploadButtonBackground = value;
                OnPropertyChanged();
            }
        }
        public ICommand OpenParselyCommand => new CommandHandler(OpenParsely);


        public bool CanOpenParsely
        {
            get => canOpenParsely; set
            {
                canOpenParsely = value;
                OnPropertyChanged();
            }
        }
        private void OpenParsely(object obj)
        {
            Process.Start(new ProcessStartInfo(parselyLink) { UseShellExecute = true });
        }
        public ICommand OpenParselyConfigCommand => new DelegateCommand(OpenParselyConfig);

        private void OpenParselyConfig()
        {
            var parselySettingsWindow = new ParselySettings();
            parselySettingsWindow.ShowDialog();
        }

        public ICommand UploadToParselyCommand => new CommandHandler(UploadToParsely);
        private void HandleParselyUploadComplete(bool status, string link)
        {
            if (status)
            {
                UploadButtonBackground = Brushes.MediumSeaGreen;
                parselyLink = link;
                CanOpenParsely = true;
            }
            else
            {
                UploadButtonBackground = Brushes.Salmon;
                CanOpenParsely = false;
            }
            Task.Run(() =>
            {
                Thread.Sleep(2000);
                UploadButtonBackground = Brushes.WhiteSmoke;
            });
        }
        private void UploadToParsely(object obj)
        {
            ParselyUploader.UploadCurrentCombat(_combatMonitorViewModel.GetActiveFile());

            UploadButtonBackground = Brushes.CornflowerBlue;
        }

        private void UpdateDataForNewTab()
        {
            ActiveRowSpan = HeaderSelectionState.CurrentlySelectedTabHeader == "Overlays" ||
                HeaderSelectionState.CurrentlySelectedTabHeader == "Leaderboards" ||
                HeaderSelectionState.CurrentlySelectedTabHeader == "Death Review" ? 3 : 2;
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

        private void AddHistoricalViewer(List<Combat> combats)
        {
            if (combats.Count == 0)
                return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                var historyGuid = Guid.NewGuid();
                var historyView = new HistoricalCombatView();
                var viewModel = new HistoricalCombatViewModel(combats);
                historyView.DataContext = viewModel;
                _activeHistoricalCombatOverviews[historyGuid] = viewModel;
                var histTab = new TabInstance() { IsHistoricalTab = true, TabContent = historyView, HeaderText = $"{combats.Last().StartTime.ToString("MM/dd")} to {combats.First().StartTime:MM/dd}", HistoryID = historyGuid };
                histTab.RequestTabClose += CloseHistoricalReview;
                ContentTabs.Add(histTab);
                SelectedTabIndex = ContentTabs.Count - 1;
            });

        }
        private void CloseHistoricalReview(TabInstance tabToClose)
        {
            var historyToRemove = _activeHistoricalCombatOverviews[tabToClose.HistoryID];
            historyToRemove.Dispose();
            ContentTabs.Remove(tabToClose);
        }
        private void MonitoringStarted(bool state)
        {
            if (state)
                Application.Current.Dispatcher.Invoke(delegate
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
            Application.Current.Dispatcher.Invoke(delegate
            {
                _overlayViewModel.CombatUpdated(updatedCombat);
                switch (HeaderSelectionState.CurrentlySelectedTabHeader)
                {
                    case "Battle Plot":
                        _plotViewModel.UpdateLivePlot(updatedCombat);
                        break;
                    case "Details":
                        _tableViewModel.AddCombat(updatedCombat);
                        break;
                    case "Combat Log":
                        _reviewViewModel.CombatSelected(updatedCombat);
                        break;
                    case "Raid Data":
                        _dataGridViewModel.UpdateCombat(updatedCombat);
                        break;
                }
            });
            
            _allViewsUpToDate = false;
        }
        private void SelectCombat(Combat selectedCombat)
        {
            UnfilteredDisplayedCombat = selectedCombat;
            UpdateViewsWithSelectedCombat(selectedCombat);
        }
        private void UpdateViewsWithSelectedCombat(Combat selectedCombat)
        {

            Application.Current.Dispatcher.Invoke(delegate
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
            Application.Current.Dispatcher.Invoke(delegate
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
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
