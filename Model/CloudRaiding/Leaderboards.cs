using SWTORCombatParser.DataStructures;
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
                //var topParse = PostgresConnection.GetTopLeaderboardForClass(bossName, className, enumVal);
                var topParse = PostgresConnection.GetTopLeaderboard(bossName, enumVal);
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
            
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                foreach(var player in combat.CharacterParticipants)
                {
                    SWTORClass playerClass;
                    if (!CombatLogStateBuilder.CurrentState.PlayerClasses.ContainsKey(player))
                    {
                        playerClass = null;
                    }
                    else
                    {
                        playerClass = CombatLogStateBuilder.CurrentState.PlayerClasses[player];
                    }
                     
                    var leaderboardEntry = new LeaderboardEntry()
                    {
                        Boss = combat.EncounterBossInfo,
                        Character = player.Name,
                        Class = playerClass == null ? "Unknown" : playerClass.Name + "/" + playerClass.Discipline,
                        Value = GetValueForLeaderboardEntry(enumVal, combat, player),
                        Type = enumVal,
                        Duration = (int)combat.DurationSeconds
                    };
                    if (leaderboardEntry.Duration > 250 || combat.EncounterBossInfo.Contains("Parsing") || combat.WasBossKilled || !combat.WasPlayerKilled[player])
                        PostgresConnection.TryAddLeaderboardEntry(leaderboardEntry);
                }
            }
        }

        private static double GetValueForLeaderboardEntry(LeaderboardEntryType role, Combat combat, Entity player)
        {
            switch (role)
            {
                case (LeaderboardEntryType.Damage):
                    return combat.DPS[player];
                case (LeaderboardEntryType.FocusDPS):
                    return combat.FocusDPS[player];
                case (LeaderboardEntryType.Healing):
                    return combat.HPS[player] + combat.PSPS[player];
                case (LeaderboardEntryType.EffectiveHealing):
                    return combat.EHPS[player] + combat.PSPS[player];
                case (LeaderboardEntryType.Mitigation):
                    return combat.TotalMitigation[player] / combat.DurationSeconds;
                default:
                    return 0;
            }
        }
    }

}
