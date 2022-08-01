using System;

namespace SWTORCombatParser.ViewModels
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
