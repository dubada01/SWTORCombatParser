using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.ViewModels.Leaderboard;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.DataGrid_Views;
using SWTORCombatParser.Views.HistoricalLogs;
using SWTORCombatParser.Views.Leaderboard_View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.Views.Battle_Review;
using SWTORCombatParser.Views.Home_Views;
using SWTORCombatParser.Views.Home_Views.PastCombatViews;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overviews;

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
        private readonly OverviewViewModel _histViewModel;
        private readonly LeaderboardViewModel _leaderboardViewModel;

        private readonly Dictionary<Guid, HistoricalCombatViewModel> _activeHistoricalCombatOverviews = new Dictionary<Guid, HistoricalCombatViewModel>();
        private int selectedTabIndex;

        public event PropertyChangedEventHandler PropertyChanged;
        public string Title { get; set; }
        public ObservableCollection<TabInstance> ContentTabs { get; set; } = new ObservableCollection<TabInstance>();
        public PastCombatsView PastCombatsView { get; set; }

        public Combat CurrentlyDisplayedCombat { get; set; }
        private bool _allViewsUpToDate;

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
            SwtorDetector.StartMonitoring();

            SwtorDetector.SwtorProcessStateChanged += ProcessChanged;
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
            _combatMonitorViewModel.OnCombatSelected += SelectCombat;
            _combatMonitorViewModel.OnCombatUnselected += UnselectCombat;
            Observable.FromEvent<Combat>(
                manager => _combatMonitorViewModel.OnLiveCombatUpdate += manager,
                manager => _combatMonitorViewModel.OnLiveCombatUpdate -= manager).Sample(TimeSpan.FromSeconds(2)).Subscribe(UpdateCombat);
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
            HeaderSelectionState.NewHeaderSelected += UpdateDataForNewTab;
        }

        private void UpdateDataForNewTab()
        {
            if(CurrentlyDisplayedCombat != null && _allViewsUpToDate == false)
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
            Application.Current.Dispatcher.Invoke(() => {
                var historyGuid = Guid.NewGuid();
                var historyView = new HistoricalCombatView();
                var viewModel = new HistoricalCombatViewModel(combats);
                historyView.DataContext = viewModel;
                _activeHistoricalCombatOverviews[historyGuid] = viewModel;
                var histTab = new TabInstance() {IsHistoricalTab=true, TabContent = historyView, HeaderText = $"{combats.Last().StartTime.ToString("MM/dd")} to {combats.First().StartTime:MM/dd}", HistoryID = historyGuid };
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
                Application.Current.Dispatcher.Invoke(delegate
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
            CurrentlyDisplayedCombat = updatedCombat;
            if (LoadingWindowFactory.MainWindowHidden)
                return;
            Application.Current.Dispatcher.Invoke(delegate
            {
                if(HeaderSelectionState.CurrentlySelectedTabHeader == "Battle Plot")
                    _plotViewModel.UpdateLivePlot(updatedCombat);
                if (HeaderSelectionState.CurrentlySelectedTabHeader == "Table")
                    _tableViewModel.AddCombat(updatedCombat);
                if (HeaderSelectionState.CurrentlySelectedTabHeader == "Histogram")
                    _histViewModel.AddCombat(updatedCombat);
                if (HeaderSelectionState.CurrentlySelectedTabHeader == "Combat Log")
                    _reviewViewModel.CombatSelected(updatedCombat);
                if (HeaderSelectionState.CurrentlySelectedTabHeader == "Raid Data")
                    _dataGridViewModel.UpdateCombat(updatedCombat);
            });
            _allViewsUpToDate = false;
        }
        private void SelectCombat(Combat selectedCombat)
        {
            CurrentlyDisplayedCombat = selectedCombat;
            Application.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.UpdateParticipants(selectedCombat);
                _plotViewModel.AddCombatPlot(selectedCombat);
                _tableViewModel.AddCombat(selectedCombat);
                _histViewModel.AddCombat(selectedCombat);
                _reviewViewModel.CombatSelected(selectedCombat);
                _dataGridViewModel.AddCombat(selectedCombat);
            });
            _allViewsUpToDate = true;
        }
        private void UnselectCombat(Combat obj)
        {
            Application.Current.Dispatcher.Invoke(delegate
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
            Application.Current.Dispatcher.Invoke(delegate
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
