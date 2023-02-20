using System;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public static class CombatSelectionMonitor
    {
        public static event Action<Combat> NewCombatSelected = delegate { };
        public static void FireNewCombat(Combat selectedCombat)
        {
            CombatIdentifier.FinalizeOverlay(selectedCombat);
            NewCombatSelected(selectedCombat);
        }
    }
}
