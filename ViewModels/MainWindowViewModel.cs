using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.resources;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.ViewModels.Leaderboard;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.ViewModels.SoftwareLogging;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.DataGrid_Views;
using SWTORCombatParser.Views.HistoricalLogs;
using SWTORCombatParser.Views.Leaderboard_View;
using SWTORCombatParser.Views.PastCombatViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private PlotViewModel _plotViewModel;
        private BattleReviewViewModel _reviewViewModel;
        private CombatMonitorViewModel _combatMonitorViewModel;
        private OverlayViewModel _overlayViewModel;
        private OverviewViewModel _tableViewModel;
        private DataGridViewModel _dataGridViewModel;
        private SoftwareLogViewModel _softwareLogViewModel;
        private OverviewViewModel _histViewModel;
        private LeaderboardViewModel _leaderboardViewModel;

        private Dictionary<Guid, HistoricalCombatViewModel> _activeHistoricalCombatOverviews = new Dictionary<Guid, HistoricalCombatViewModel>();
        private int selectedTabIndex;

        public event PropertyChangedEventHandler PropertyChanged;
        public string Title { get; set; }
        public ObservableCollection<TabInstance> ContentTabs { get; set; } = new ObservableCollection<TabInstance>();
        public PastCombatsView PastCombatsView { get; set; }

        

        public int SelectedTabIndex
        {
            get => selectedTabIndex;
            set
            {
                selectedTabIndex = value;
                OnPropertyChanged();
            }
        }
        public MainWindowViewModel()
        {
            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version}";
            ClassIdentifier.InitializeAvailableClasses();
            RaidNameLoader.LoadAllRaidNames();
            SWTORDetector.StartMonitoring();

            SWTORDetector.SWTORProcessStateChanged += ProcessChanged;
            MainWindowClosing.Hiding += () =>
            {
                if (!SWTORDetector.SwtorRunning)
                    _overlayViewModel.HideOverlays();
                if (SWTORDetector.SwtorRunning && !_combatMonitorViewModel.LiveParseActive)
                    _combatMonitorViewModel.EnableLiveParse();
                if (SWTORDetector.SwtorRunning)
                    _overlayViewModel.OverlaysLocked = true;
            };
            _combatMonitorViewModel = new CombatMonitorViewModel();
            _combatMonitorViewModel.OnCombatSelected += SelectCombat;
            _combatMonitorViewModel.OnCombatUnselected += UnselectCombat;
            Observable.FromEvent<Combat>(
                manager => _combatMonitorViewModel.OnLiveCombatUpdate += manager,
                manager => _combatMonitorViewModel.OnLiveCombatUpdate -= manager).Sample(TimeSpan.FromSeconds(2)).Subscribe(update => UpdateCombat(update));
            _combatMonitorViewModel.LiveCombatFinished += UpdateCombat;
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
            ContentTabs.Add(new TabInstance() { TabContent = tableView, HeaderText = "Table" });

            _softwareLogViewModel = new SoftwareLogViewModel();

            _histViewModel = new HistogramVeiewModel();
            var histView = new OverviewView(_histViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = histView, HeaderText = "Histogram" });

            _reviewViewModel = new BattleReviewViewModel();
            ContentTabs.Add(new TabInstance() { TabContent = new BattleReviewView(_reviewViewModel),HeaderText = "Combat Log" });

            _overlayViewModel = new OverlayViewModel();
            var overlayView = new OverlayView(_overlayViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = overlayView, HeaderText = "Overlays" });

            _leaderboardViewModel = new LeaderboardViewModel();
            var leaderboardView = new LeaderboardView(_leaderboardViewModel);
            ContentTabs.Add(new TabInstance { TabContent = leaderboardView, HeaderText = "Leaderboards" });

            SelectedTabIndex = 0;

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
            App.Current.Dispatcher.Invoke(() => {
                var historyGuid = Guid.NewGuid();
                var historyView = new HistoricalCombatView();
                var viewModel = new HistoricalCombatViewModel(combats);
                historyView.DataContext = viewModel;
                _activeHistoricalCombatOverviews[historyGuid] = viewModel;
                var histTab = new TabInstance() {IsHistoricalTab=true, TabContent = historyView, HeaderText = $"{combats.Last().StartTime.ToString("MM/dd")} to {combats.First().StartTime.ToString("MM/dd")}", HistoryID = historyGuid };
                histTab.RequestTabClose += CloseHistoricalReview;
                ContentTabs.Add(histTab);
                SelectedTabIndex = ContentTabs.Count-1;
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
            if(state)
                App.Current.Dispatcher.Invoke(delegate
                {
                    _plotViewModel.Reset();
                    _tableViewModel.Reset();
                    _histViewModel.Reset();
                    _dataGridViewModel.Reset();
                });
            _overlayViewModel.LiveParseStarted(state);
        }

        private void UpdateCombat(Combat updatedCombat)
        {
            if (LoadingWindowFactory.MainWindowHidden)
                return;
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.UpdateLivePlot(updatedCombat);
                _tableViewModel.AddCombat(updatedCombat);
                _histViewModel.AddCombat(updatedCombat);
                _reviewViewModel.CombatSelected(updatedCombat);
                _dataGridViewModel.UpdateCombat(updatedCombat);
            });
        }
        private void SelectCombat(Combat selectedCombat)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.UpdateParticipants(selectedCombat);
                _plotViewModel.AddCombatPlot(selectedCombat);
                _tableViewModel.AddCombat(selectedCombat);
                _histViewModel.AddCombat(selectedCombat);
                _reviewViewModel.CombatSelected(selectedCombat);
                _dataGridViewModel.AddCombat(selectedCombat);
            });

        }
        private void UnselectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.RemoveCombatPlot(obj);
                _tableViewModel.RemoveCombat(obj);
                _histViewModel.RemoveCombat(obj);
                _dataGridViewModel.RemoveCombat(obj);
            });
        }
        private Entity localEntity;
        private void LocalPlayerChanged(Entity obj)
        {
            if (localEntity == obj)
                return;
            App.Current.Dispatcher.Invoke(delegate
            {
                if(localEntity != obj)
                {
                    _plotViewModel.Reset();
                    _tableViewModel.Reset();
                    _histViewModel.Reset();
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
