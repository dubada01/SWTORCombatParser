using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class Leaderboards
    {
        public static Dictionary<LeaderboardEntryType, (string, double)> TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
        public static Dictionary<LeaderboardEntryType, List<LeaderboardEntry>> CurrentFightLeaderboard = new Dictionary<LeaderboardEntryType, List<LeaderboardEntry>>();
        public static Dictionary<LeaderboardEntryType, (string, double)> GetTopLeaderboardEntries(Combat newCombat, bool filterClass = false)
        {
            if (TopLeaderboards.Count > 0)
                return TopLeaderboards;
            var bossName = newCombat.EncounterBossInfo;
            var localPlayerClass = CombatLogStateBuilder.CurrentState.PlayerClasses[newCombat.LocalPlayer];
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;

            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                LeaderboardEntry topParse;
                if(filterClass)
                    topParse = PostgresConnection.GetTopLeaderboardForClass(bossName, className, enumVal);
                else
                    topParse = PostgresConnection.GetTopLeaderboard(bossName, enumVal);
                if (string.IsNullOrEmpty(topParse.Character))
                    continue;
                TopLeaderboards[enumVal] = (topParse.Character, topParse.Value);
            }
            return TopLeaderboards;
        }
        public static void GetCurrentLeaderboard(Combat newCombat, bool filterClass = false)
        {
            var bossName = newCombat.EncounterBossInfo;
            var localPlayerClass = CombatLogStateBuilder.CurrentState.PlayerClasses[newCombat.LocalPlayer];
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                if (filterClass)
                    CurrentFightLeaderboard[enumVal] = PostgresConnection.GetEntriesForBossWithClass(bossName, className, enumVal);
                else
                    CurrentFightLeaderboard[enumVal] = PostgresConnection.GetEntriesForBossOfType(bossName, enumVal);
            }

        }
        public static Dictionary<Entity,Dictionary<LeaderboardEntryType, double>> GetyPlayerLeaderboardStandings(Combat newCombat, bool filterClass = false)
        {
            if (CurrentFightLeaderboard.Count == 0)
                GetCurrentLeaderboard(newCombat, filterClass);
            var returnData = new Dictionary<Entity, Dictionary<LeaderboardEntryType, double>>();
            var bossName = newCombat.EncounterBossInfo;
            var localPlayerClass = CombatLogStateBuilder.CurrentState.PlayerClasses[newCombat.LocalPlayer];
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            foreach(var participant in newCombat.CharacterParticipants)
            {
                if (filterClass)
                {
                    var participantClass = CombatLogStateBuilder.CurrentState.PlayerClasses[participant];
                    var participantClassInfo = participantClass == null ? "Unknown" : participantClass.Name + "/" + participantClass.Discipline;
                    if (participantClassInfo != className)
                    {
                        returnData[participant] = null;
                        continue;
                    }
                }
                returnData[participant] = new Dictionary<LeaderboardEntryType, double>();


                foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
                {
                    var parses = CurrentFightLeaderboard[enumVal];
                    if (!parses.Any(p => p.Character == participant.Name))
                        returnData[participant][enumVal] = 0;
                    else
                    {
                        var currentValue = GetValueForLeaderboardEntry(enumVal, newCombat, participant);
                        var parsesWithVal = parses.Select(v=>v.Value).ToList();
                        parsesWithVal.Add(currentValue);
                        returnData[participant][enumVal] = parsesWithVal.OrderByDescending(v=>v).ToList().IndexOf(currentValue);
                    }
                }
            }
            return returnData;

        }
        public static void Reset()
        {
            TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
            CurrentFightLeaderboard = new Dictionary<LeaderboardEntryType, List<LeaderboardEntry>>();
        }

        public static void TryAddLeaderboardEntry(Combat combat)
        {
            //// REMOVE WITH 7.0
            if (combat.WasPlayerKilled[combat.LocalPlayer])
                return;
            
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
                    return combat.MitigationPercent[player];
                default:
                    return 0;
            }
        }
    }

}
