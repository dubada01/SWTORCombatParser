using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Phases;
using System;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring
{
    public static class CombatSelectionMonitor
    {
        public static event Action<Combat> CombatSelected = delegate { };
        // Row selection is distinct from automatic updates/completion of a live combat.
        public static event Action<Combat, bool> SelectedCombatChanged = delegate { };
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
            PhaseManager.PublishSelection(combat, null, isCurrentCombat: true);
            CombatSelected(combat);
        }
        public static void SelectCombat(Combat combat, bool isCurrentCombat)
            => SelectCombat(combat, isCurrentCombat, null);

        internal static void SelectCombat(Combat combat, bool isCurrentCombat, PhaseManager.PreparedSelection? phases)
        {
            _hasSetLeaderboard = false;
            // Historical review must not replace the shell that receives the next live batch.
            if (isCurrentCombat || !CombatMonitorViewModel.IsLiveParseActive())
                CombatIdentifier.CurrentCombat = combat;
            PhaseManager.PublishSelection(combat, phases, isCurrentCombat);
            CombatSelected(combat);
            SelectedCombatChanged(combat, isCurrentCombat);
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
