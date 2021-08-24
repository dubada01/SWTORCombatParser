using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.SoftwareLogging;
using SWTORCombatParser.Views;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel:INotifyPropertyChanged
    {
        private PlotViewModel _plotViewModel;
        private CombatMonitorViewModel _combatMonitorViewModel;
        private OverlayViewModel _overlayViewModel;
        private TableViewModel _tableViewModel;
        private SoftwareLogViewModel _softwareLogViewModel;
        private RaidViewModel _raidViewModel;

        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindowViewModel()
        {
            ClassIdentifier.InitializeAvailableClasses();
            RaidNameLoader.LoadAllRaidNames();

            _plotViewModel = new PlotViewModel();
            GraphView = new GraphView(_plotViewModel);

            _combatMonitorViewModel = new CombatMonitorViewModel();
            _combatMonitorViewModel.OnCombatSelected += SelectCombat;
            _combatMonitorViewModel.OnCombatUnselected += UnselectCombat;
            _combatMonitorViewModel.OnLiveCombatUpdate += UpdateLivePlot;
            _combatMonitorViewModel.OnMonitoringStarted += MonitoringStarted;
            _combatMonitorViewModel.OnCharacterNameIdentified += CharacterNameId;
            _combatMonitorViewModel.OnNewLog += NewSoftwareLog;
            
            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);

            _overlayViewModel = new OverlayViewModel();
            OverlayView = new OverlayView(_overlayViewModel);

            _tableViewModel = new TableViewModel();
            TableView = new TableView(_tableViewModel);

            _softwareLogViewModel = new SoftwareLogViewModel();
            SoftwareLogView = new LogsView(_softwareLogViewModel);

            _raidViewModel = new RaidViewModel();
            _raidViewModel.RaidStateChanged += RaidingStateChaneged;
            _raidViewModel.OnNewRaidCombatFinished += RaidCombatAdded;
            _raidViewModel.OnRaidParticipantSelected += RaidPariticpantSelected;
            _raidViewModel.OnNewRaidCombatStarted += RaidCombatStarted;
            RaidView = new RaidView(_raidViewModel);

            CombatLogParser.OnNewLog += NewSoftwareLog;


        }
        public OverlayView OverlayView { get; set; }
        public RaidView RaidView { get; set; }
        public GraphView GraphView { get; set; }
        public TableView TableView { get; set; }
        public LogsView SoftwareLogView { get; set; }
        public PastCombatsView PastCombatsView { get; set; }

        public SolidColorBrush RaidingActiveColor { get; set; }
        private void RaidingStateChaneged(bool state)
        {
            if (state)
                _combatMonitorViewModel.RaidingStarted();
            else
                _combatMonitorViewModel.RaidingStopped();
            RaidingActiveColor = state ? new SolidColorBrush(Color.FromRgb(0,165,156)) : Brushes.Transparent;
            OnPropertyChanged("RaidingActiveColor");
        }
        private void RaidPariticpantSelected(string name)
        {
            App.Current.Dispatcher.Invoke(delegate
            {
                _plotViewModel.SetCharacterName(name);
                _plotViewModel.Reset();
                _combatMonitorViewModel.ClearCombats();
            });
        }
        private void RaidCombatStarted()
        {
            _combatMonitorViewModel.StartCombat("Raid Group");
        }
        private void RaidCombatAdded(Combat combatAdded)
        {
            if (combatAdded == null)
                return;
            App.Current.Dispatcher.Invoke(delegate
            {
                _combatMonitorViewModel.AddRaidCombat(combatAdded);
            });
        }
        private void MonitoringStarted()
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.Reset();
                _tableViewModel.Reset();
            });
        }
        private void UnselectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.RemoveCombatPlot(obj);
                _tableViewModel.RemoveCombatLogs(obj.Logs);
            });
        }

        private void UpdateLivePlot(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.UpdateLivePlot(obj);
                _tableViewModel.AddCombatLogs(obj.Logs);

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
                _tableViewModel.AddCombatLogs(obj.Logs);

            });
            
        }
        private void UpdateDisplayedData(AxisLimits newAxisLimits)
        {
            
        }
        private void CharacterNameId(string obj)
        {
            _plotViewModel.SetCharacterName(obj);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
