using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities.Encounter_Selection
{
    public static class EncounterSelectionFactory
    {
        public static EncounterSelectionView GetEncounterSelectionView(bool showPlayercount = true)
        {
            var vm = new EncounterSelectionViewModel(showPlayercount);
            var view = new EncounterSelectionView();
            view.DataContext = vm;
            return view;
        }
    }
}
