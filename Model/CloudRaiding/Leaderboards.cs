using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class Leaderboards
    {
        public static Dictionary<LeaderboardEntryType, (string, double)> TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
        public static Dictionary<LeaderboardEntryType, (string, double)> GetLeaderboardInfo(Combat newCombat)
        {
            if (TopLeaderboards.Count > 0)
                return TopLeaderboards;
            var bossName = newCombat.EncounterBossInfo;
            var localPlayerClass = CombatLogStateBuilder.CurrentState.PlayerClasses[newCombat.LocalPlayer];
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;

            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                var topParse = PostgresConnection.GetTopLeaderboard(bossName, className, enumVal);
                if (string.IsNullOrEmpty(topParse.Character))
                    continue;
                TopLeaderboards[enumVal] = (topParse.Character, topParse.Value);
            }
            return TopLeaderboards;
        }
        public static void Reset()
        {
            TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
        }

        public static void TryAddLeaderboardEntry(Combat combat)
        {
            var localPlayerClass = CombatLogStateBuilder.CurrentState.PlayerClasses[combat.LocalPlayer];
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                var leaderboardEntry = new LeaderboardEntry()
                {
                    Boss = combat.EncounterBossInfo,
                    Character = combat.LocalPlayer.Name,
                    Class = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline,
                    Value = GetValueForLeaderboardEntry(enumVal, combat),
                    Type = enumVal,
                    Duration = (int)combat.DurationSeconds
                };
                if (leaderboardEntry.Duration > 250 || combat.EncounterBossInfo.Contains("Parsing"))
                    PostgresConnection.TryAddLeaderboardEntry(leaderboardEntry);
            }
        }

        private static double GetValueForLeaderboardEntry(LeaderboardEntryType role, Combat combat)
        {
            switch (role)
            {
                case (LeaderboardEntryType.Damage):
                    return combat.DPS[combat.LocalPlayer];
                case (LeaderboardEntryType.FocusDPS):
                    return combat.FocusDPS[combat.LocalPlayer];
                case (LeaderboardEntryType.Healing):
                    return combat.HPS[combat.LocalPlayer] + combat.PSPS[combat.LocalPlayer];
                case (LeaderboardEntryType.EffectiveHealing):
                    return combat.EHPS[combat.LocalPlayer] + combat.PSPS[combat.LocalPlayer];
                case (LeaderboardEntryType.Mitigation):
                    return combat.TotalMitigation[combat.LocalPlayer] / combat.DurationSeconds;
                default:
                    return 0;
            }
        }
    }

}
