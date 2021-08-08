using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.ViewModels.SoftwareLogging;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel
    {
        private PlotViewModel _plotViewModel;
        private CombatMonitorViewModel _combatMonitorViewModel;
        private CombatMetaDataViewModel _combatMetaDataViewModel;
        private TableViewModel _tableViewModel;
        private SoftwareLogViewModel _softwareLogViewModel;
        public MainWindowViewModel()
        {
            ClassIdentifier.InitializeAvailableClasses();
            RaidNameLoader.LoadAllRaidNames();

            _plotViewModel = new PlotViewModel();
            _plotViewModel.OnPlotMoved += UpdateDisplayedData;
            GraphView = new GraphView(_plotViewModel);

            _combatMonitorViewModel = new CombatMonitorViewModel();
            _combatMonitorViewModel.OnCombatSelected += SelectCombat;
            _combatMonitorViewModel.OnCombatUnselected += UnselectCombat;
            _combatMonitorViewModel.OnLiveCombatUpdate += UpdateLivePlot;
            
            _combatMonitorViewModel.OnCharacterNameIdentified += CharacterNameId;
            _combatMonitorViewModel.OnNewLog += NewSoftwareLog;
            PastCombatsView = new PastCombatsView(_combatMonitorViewModel);

            _combatMetaDataViewModel = new CombatMetaDataViewModel();
            _combatMetaDataViewModel.OnEffectSelected += NewEffectSelected;
            CombatMetaDataView = new CombatMetaDataView(_combatMetaDataViewModel);

            _tableViewModel = new TableViewModel();
            TableView = new TableView(_tableViewModel);

            _softwareLogViewModel = new SoftwareLogViewModel();
            SoftwareLogView = new LogsView(_softwareLogViewModel);
        }
        public GraphView GraphView { get; set; }
        public TableView TableView { get; set; }
        public LogsView SoftwareLogView { get; set; }
        public PastCombatsView PastCombatsView { get; set; }
        public CombatMetaDataView CombatMetaDataView { get; set; }
        private void UnselectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.RemoveCombatPlot(obj);
                _tableViewModel.RemoveCombatLogs(obj.Logs);
                //_combatMetaDataViewModel.PopulateCombatMetaDatas(obj);
            });
        }

        private void UpdateLivePlot(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate {
                _plotViewModel.UpdateLivePlot(obj);
                _tableViewModel.AddCombatLogs(obj.Logs);
                _combatMetaDataViewModel.PopulateCombatMetaDatas(obj);
            });
        }
        private void NewSoftwareLog(string log)
        {
            var newLog = new SoftwareLogInstance() { TimeStamp = DateTime.Now, Message = log };
            _softwareLogViewModel.AddNewLog(newLog);
        }
        private void NewEffectSelected(List<CombatModifier> obj)
        {
            _plotViewModel.HighlightEffect(obj);
        }
        private void SelectCombat(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate{
                _plotViewModel.AddCombatPlot(obj);
                _tableViewModel.AddCombatLogs(obj.Logs);
                _combatMetaDataViewModel.PopulateCombatMetaDatas(obj);
            });
            
        }
        private void UpdateDisplayedData(AxisLimits newAxisLimits)
        {
            _combatMetaDataViewModel.UpdateBasedOnVisibleData(newAxisLimits);
        }
        private void CharacterNameId(string obj)
        {
            _combatMetaDataViewModel.CharacterName = obj;
        }
    }
}
