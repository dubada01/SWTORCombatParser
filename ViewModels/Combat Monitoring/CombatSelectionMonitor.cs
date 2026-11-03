using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using System;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public static class CombatSelectionMonitor
    {
        public static event Action<Combat> CombatSelected = delegate { };
        public static event Action<Combat> OnInProgressCombatSelected = delegate { };
        public static event Action<Combat> PhaseSelected = delegate { };

        private static bool _hasSetLeaderboard;

        public static void SelectPhase(Combat combat)
        {
            PhaseSelected(combat);
        }
        public static void InProgressCombatSeleted(Combat combat)
        {
            CombatIdentifier.CurrentCombat = combat;
            OnInProgressCombatSelected(combat);
        }
        public static void SelectCompleteCombat(Combat combat)
        {
            _hasSetLeaderboard = false;
            CombatIdentifier.CurrentCombat = combat;
            CombatSelected(combat);
        }
        public static void CheckForLeaderboardOnSelectedCombat(Combat combat)
        {
            if (_hasSetLeaderboard)
                return;
            _hasSetLeaderboard = true;
            Leaderboards.UpdateOverlaysWithNewLeaderboard(combat,true);
        }
        public static event Action<Combat> CombatDeselected = delegate { };
        public static void DeselectCombat(Combat combat)
        {
            CombatDeselected(combat);
        }
    }
}
