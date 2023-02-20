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
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.Views.Battle_Review;
using SWTORCombatParser.Views.Home_Views;
using SWTORCombatParser.Views.Home_Views.PastCombatViews;
using SWTORCombatParser.Views.Overlay;
using SWTORCombatParser.Views.Overviews;
using System.Windows.Input;
using SWTORCombatParser.Model.CloudRaiding;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.Security.Policy;

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
            EncounterLoader.LoadAllEncounters();
            //SwtorDetector.StartMonitoring();
            TimerController.Init();
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
            Observable.FromEvent<double>(
                manager => _combatMonitorViewModel.OnNewLogTimeOffsetMs += manager,
                manager => _combatMonitorViewModel.OnNewLogTimeOffsetMs -= manager).Buffer(TimeSpan.FromSeconds(2)).Subscribe(UpdateLogTimeOffset);
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
            ContentTabs.Add(new TabInstance() { TabContent = new BattleReviewView(_reviewViewModel), HeaderText = "Combat Log" });

            _overlayViewModel = new OverlayViewModel();
            var overlayView = new OverlayView(_overlayViewModel);
            var overlayTab = new TabInstance()
            { TabContent = overlayView, HeaderText = "Overlays", IsOverlaysTab = true };
            _overlayViewModel.OverlayLockStateChanged += overlayTab.UpdateLockIcon;
            ContentTabs.Add(overlayTab);

            _leaderboardViewModel = new LeaderboardViewModel();
            var leaderboardView = new LeaderboardView(_leaderboardViewModel);
            ContentTabs.Add(new TabInstance { TabContent = leaderboardView, HeaderText = "Leaderboards" });

            SelectedTabIndex = 0;
            HeaderSelectionState.NewHeaderSelected += UpdateDataForNewTab;
            ParselyUploader.UploadCompleted += HandleParselyUploadComplete;
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
                    _histViewModel.Reset();
                    _dataGridViewModel.Reset();
                    _reviewViewModel.Reset();
                });
            _overlayViewModel.LiveParseStarted(state);
        }

        private void UpdateLogTimeOffset(IList<double> logOffsetFor2Seconds)
        {
            if (!logOffsetFor2Seconds.Any())
                return;
            var average = logOffsetFor2Seconds.Average() / 1000d;
            _combatMonitorViewModel.CurrentLogOffsetMs = Math.Round(average, 1);
        }
        private void UpdateCombat(Combat updatedCombat)
        {
            CurrentlyDisplayedCombat = updatedCombat;
            if (LoadingWindowFactory.MainWindowHidden)
                return;
            Application.Current.Dispatcher.Invoke(delegate
            {
                switch (HeaderSelectionState.CurrentlySelectedTabHeader)
                {
                    case "Battle Plot":
                        _plotViewModel.UpdateLivePlot(updatedCombat);
                        break;
                    case "Table":
                        _tableViewModel.AddCombat(updatedCombat);
                        break;
                    case "Histogram":
                        _histViewModel.AddCombat(updatedCombat);
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
