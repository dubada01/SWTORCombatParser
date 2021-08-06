using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Plotting;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.ViewModels
{
    public class MainWindowViewModel
    {
        private PlotViewModel _plotViewModel;
        private CombatMonitorViewModel _pastCombatsViewModel;
        private CombatMetaDataViewModel _combatMetaDataViewModel;
        public MainWindowViewModel()
        {
            ClassIdentifier.InitializeAvailableClasses();
            _plotViewModel = new PlotViewModel();
            GraphView = new GraphView(_plotViewModel);
            _pastCombatsViewModel = new CombatMonitorViewModel();
            PastCombatsView = new PastCombatsView(_pastCombatsViewModel);
            _combatMetaDataViewModel = new CombatMetaDataViewModel();
            CombatMetaDataView = new CombatMetaDataView(_combatMetaDataViewModel);
            _pastCombatsViewModel.OnNewCombat += NewCombatReceived;
            _pastCombatsViewModel.OnCharacterNameIdentified += CharacterNameId;
        }



        public GraphView GraphView { get; set; }
        public TableView TableView { get; set; }
        public PastCombatsView PastCombatsView { get; set; }
        public CombatMetaDataView CombatMetaDataView { get; set; }
        private void NewCombatReceived(Combat obj)
        {
            App.Current.Dispatcher.Invoke(delegate{
                _plotViewModel.PlotData(obj);
                _combatMetaDataViewModel.PopulateCombatMetaDatas(obj);
            });
            
        }
        private void CharacterNameId(string obj)
        {
            _combatMetaDataViewModel.CharacterName = obj;
        }
    }
}
