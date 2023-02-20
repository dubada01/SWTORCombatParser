using System.Collections.Generic;

namespace SWTORCombatParser.Utilities.Encounter_Selection
{
    public static class EncounterSelectionFactory
    {
        public static EncounterSelectionView GetEncounterSelectionView(bool showPlayercount = true, List<string> populatedEncounterNames = null)
        {
            var vm = new EncounterSelectionViewModel(showPlayercount, populatedEncounterNames);
            var view = new EncounterSelectionView();
            view.DataContext = vm;
            return view;
        }
    }
}
