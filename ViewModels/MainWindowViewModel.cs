using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overviews;
using SWTORCombatParser.ViewModels.SoftwareLogging;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel:INotifyPropertyChanged
    {
        private PlotViewModel _plotViewModel;
        private CombatMonitorViewModel _combatMonitorViewModel;
        private OverlayViewModel _overlayViewModel;
        private OverviewViewModel _tableViewModel;
        private SoftwareLogViewModel _softwareLogViewModel;
        private OverviewViewModel _histViewModel;
       // private RaidViewModel _raidViewModel;

        public event PropertyChangedEventHandler PropertyChanged;
        public string Title { get; set; }
        public MainWindowViewModel()
        {
            Title = $"{Assembly.GetExecutingAssembly().GetName().Name} v{Assembly.GetExecutingAssembly().GetName().Version}";
            ClassIdentifier.InitializeAvailableClasses();
            RaidNameLoader.LoadAllRaidNames();

            _plotViewModel = new PlotViewModel();
            GraphView = new GraphView(_plotViewModel);

            _combatMonitorViewModel = new CombatMonitorViewModel();
            _combatMonitorViewModel.OnCombatSelected += SelectCombat;
            _combatMonitorViewModel.OnCombatUnselected += UnselectCombat;
            _combatMonitorViewModel.OnLiveCombatUpdate += UpdateLivePlot;
            _combatMonitorViewModel.OnMonitoringStarted += MonitoringStarted;
            _combatMonitorViewModel.ParticipantsUpdated += UpdateAvailableParticipants;
            _combatMonitorViewModel.OnNewLog += NewSoftwareLog;
            
            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);

            _overlayViewModel = new OverlayViewModel();
            OverlayView = new OverlayView(_overlayViewModel);

            _tableViewModel = new TableViewModel();
            TableView = new OverviewView(_tableViewModel);

            _softwareLogViewModel = new SoftwareLogViewModel();
            SoftwareLogView = new LogsView(_softwareLogViewModel);

            _histViewModel = new HistogramVeiewModel();
            HistogramView = new OverviewView(_histViewModel);

            CombatLogParser.OnNewLog += NewSoftwareLog;


        }
        public OverlayView OverlayView { get; set; }
        //public RaidView RaidView { get; set; }
        public GraphView GraphView { get; set; }
        public OverviewView TableView { get; set; }
        public OverviewView HistogramView { get; set; }
        public LogsView SoftwareLogView { get; set; }
        public PastCombatsView PastCombatsView { get; set; }


        private void MonitoringStarted()
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.Reset();
                _tableViewModel.Reset();
                _histViewModel.Reset();
            });
        }

        private void UpdateLivePlot(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.UpdateLivePlot(obj);
                _tableViewModel.AddCombat(obj);
                _histViewModel.AddCombat(obj);

            });
        }
        private void NewSoftwareLog(string log)
        {
            var newLog = new SoftwareLogInstance() { TimeStamp = DateTime.Now, Message = log };
            _softwareLogViewModel.AddNewLog(newLog);
        }
        private void SelectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate{
                _plotViewModel.AddCombatPlot(obj);
                _tableViewModel.AddCombat(obj);
                _histViewModel.AddCombat(obj);
            });

        }
        private void UnselectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.RemoveCombatPlot(obj);
                _tableViewModel.RemoveCombat(obj);
                _histViewModel.RemoveCombat(obj);
            });
        }
        private void UpdateDisplayedData(AxisLimits newAxisLimits)
        {
            
        }
        private void UpdateAvailableParticipants(List<Entity> obj)
        {
            _plotViewModel.UpdateParticipants(obj);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
