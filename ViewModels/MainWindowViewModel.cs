﻿using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.resources;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.HistoricalLogs;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.ViewModels.SoftwareLogging;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.HistoricalLogs;
using SWTORCombatParser.Views.PastCombatViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        private SoftwareLogViewModel _softwareLogViewModel;
        private OverviewViewModel _histViewModel;
        private Dictionary<Guid, HistoricalCombatViewModel> _activeHistoricalCombatOverviews = new Dictionary<Guid, HistoricalCombatViewModel>();
        private int selectedTabIndex;

        public event PropertyChangedEventHandler PropertyChanged;
        public string Title { get; set; }
        public ObservableCollection<TabInstance> ContentTabs { get; set; } = new ObservableCollection<TabInstance>();
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

            SWTORDetector.SWTORProcessStateChanged += ProcessChanged;
            MainWindowClosing.Hiding += () =>
            {
                if (!SWTORDetector.SwtorRunning)
                    _overlayViewModel.ResetOverlays();
                if (SWTORDetector.SwtorRunning && !_combatMonitorViewModel.LiveParseActive)
                    _combatMonitorViewModel.EnableLiveParse();
            };

            _plotViewModel = new PlotViewModel();
            var graphView = new GraphView(_plotViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = graphView, HeaderText = "Battle Plot" });

            _combatMonitorViewModel = new CombatMonitorViewModel();
            _combatMonitorViewModel.OnCombatSelected += SelectCombat;
            _combatMonitorViewModel.OnCombatUnselected += UnselectCombat;
            _combatMonitorViewModel.OnLiveCombatUpdate += UpdateCombat;
            _combatMonitorViewModel.OnMonitoringStarted += MonitoringStarted;
            _combatMonitorViewModel.ParticipantsUpdated += UpdateAvailableParticipants;
            _combatMonitorViewModel.LocalPlayerId += LocalPlayerChanged;
            _combatMonitorViewModel.OnHistoricalCombatsParsed += AddHistoricalViewer;
            //_combatMonitorViewModel.OnNewLog += NewSoftwareLog;

            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);



            _tableViewModel = new TableViewModel();
            var tableView = new OverviewView(_tableViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = tableView, HeaderText = "Table" });

            //_softwareLogViewModel = new SoftwareLogViewModel();
            //SoftwareLogView = new LogsView(_softwareLogViewModel);

            _histViewModel = new HistogramVeiewModel();
            var histView = new OverviewView(_histViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = histView, HeaderText = "Histogram" });

            _reviewViewModel = new BattleReviewViewModel();
            ContentTabs.Add(new TabInstance() { TabContent = new BattleReviewView(_reviewViewModel),HeaderText = "Combat Log" });

            _overlayViewModel = new OverlayViewModel();
            var overlayView = new OverlayView(_overlayViewModel);
            ContentTabs.Add(new TabInstance() { TabContent = overlayView, HeaderText = "Overlays" });

            //CombatLogParser.OnNewLog += NewSoftwareLog;

            SelectedTabIndex = 0;

        }

        private void ProcessChanged(bool obj)
        {
            //if (!LoadingWindowFactory.MainWindowHidden)
            //    return;
            if (obj)
            {
                if (LoadingWindowFactory.MainWindowHidden)
                {
                    _combatMonitorViewModel.EnableLiveParse(true);
                    Task.Run(() => {
                        Thread.Sleep(1000);
                        _overlayViewModel.OverlaysLocked = true;
                    });
                    
                }
            }
            else
            {
                _combatMonitorViewModel.DisableLiveParse();
                if (LoadingWindowFactory.MainWindowHidden)
                    _overlayViewModel.ResetOverlays();
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
        public PastCombatsView PastCombatsView { get; set; }


        private void MonitoringStarted()
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.Reset();
                _tableViewModel.Reset();
                _histViewModel.Reset();
            });
        }

        private void UpdateCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.UpdateLivePlot(obj);
                _tableViewModel.AddCombat(obj);
                _histViewModel.AddCombat(obj);
                _reviewViewModel.CombatSelected(obj);
            });
        }
        private void NewSoftwareLog(string log)
        {
            var newLog = new SoftwareLogInstance() { TimeStamp = DateTime.Now, Message = log };
            _softwareLogViewModel.AddNewLog(newLog);
        }
        private void SelectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.AddCombatPlot(obj);
                _tableViewModel.AddCombat(obj);
                _histViewModel.AddCombat(obj);
                _reviewViewModel.CombatSelected(obj);
            });

        }
        private void UnselectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.RemoveCombatPlot(obj);
                _tableViewModel.RemoveCombat(obj);
                _histViewModel.RemoveCombat(obj);
            });
        }
        private void UpdateAvailableParticipants(List<Entity> obj)
        {
            _plotViewModel.UpdateParticipants(obj);
            var localPlayer = obj.FirstOrDefault(c => c.IsLocalPlayer);
            if (localPlayer != null)
            {
                _overlayViewModel.LocalPlayerIdentified(localPlayer);
            }
        }
        private Entity localEntity;
        private void LocalPlayerChanged(Entity obj)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                if(localEntity != obj)
                {
                    _plotViewModel.Reset();
                    _tableViewModel.Reset();
                    _histViewModel.Reset();
                    _overlayViewModel.LocalPlayerIdentified(obj);
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
