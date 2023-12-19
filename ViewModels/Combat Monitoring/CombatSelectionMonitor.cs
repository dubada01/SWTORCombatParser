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
        private static string _selectedCombatBoss;

        public static void SelectPhase(Combat combat)
        {
            PhaseSelected(combat);
        }
        public static void InProgressCombatSeleted(Combat combat)
        {
            _selectedCombatBoss = combat.EncounterBossDifficultyParts.Item1;
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
            if (_hasSetLeaderboard || combat.EncounterBossDifficultyParts.Item1 == _selectedCombatBoss)
                return;
            _selectedCombatBoss = combat.EncounterBossDifficultyParts.Item1;
            _hasSetLeaderboard = true;
            Leaderboards.UpdateOverlaysWithNewLeaderboard(combat);
        }
        public static event Action<Combat> CombatDeselected = delegate { };
        public static void DeselectCombat(Combat combat)
        {
            CombatDeselected(combat);
        }
    }
}
