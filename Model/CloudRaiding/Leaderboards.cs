using MoreLinq;
using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public enum LeaderboardType
    {
        AllDiciplines,
        LocalDicipline,
        Off
    }
    public static class Leaderboards
    {
        public static string _leaderboardVersion = "1";
        public static object _updateLock = new object();
        public static LeaderboardType CurrentLeaderboardType;
        public static event Action<Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>>> LeaderboardStandingsAvailable = delegate { };
        public static event Action<Dictionary<LeaderboardEntryType, (string, double)>> TopLeaderboardEntriesAvailable = delegate { };
        public static event Action<LeaderboardType> LeaderboardTypeChanged = delegate { };
        public static Dictionary<LeaderboardEntryType, (string, double)> TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
        public static Dictionary<LeaderboardEntryType, List<LeaderboardEntry>> CurrentFightLeaderboard = new Dictionary<LeaderboardEntryType, List<LeaderboardEntry>>();
        public static Combat CurrentCombat;
        public static void UpdateLeaderboardType(LeaderboardType type)
        {
            CurrentLeaderboardType = type;
            CurrentFightLeaderboard.Clear();
            LeaderboardTypeChanged(CurrentLeaderboardType);
            if (CurrentCombat == null)
                return;
            StartGetPlayerLeaderboardStandings(CurrentCombat);
            StartGetTopLeaderboardEntries(CurrentCombat);
        }
        public static void StartGetTopLeaderboardEntries(Combat newCombat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            CurrentCombat = newCombat;
            if (TopLeaderboards.Count > 0)
                TopLeaderboardEntriesAvailable(TopLeaderboards);
            if (!CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                return;
            var bossName = newCombat.EncounterBossInfo;
            var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetCharacterClassAtTime(newCombat.LocalPlayer,newCombat.StartTime);
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;

            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                LeaderboardEntry topParse;
                if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                    topParse = PostgresConnection.GetTopLeaderboardForClass(bossName, encounterName, className, enumVal);
                else
                    topParse = PostgresConnection.GetTopLeaderboard(bossName, encounterName, enumVal);
                if (string.IsNullOrEmpty(topParse.Character))
                    continue;
                TopLeaderboards[enumVal] = (topParse.Character, topParse.Value);
            }
            TopLeaderboardEntriesAvailable(TopLeaderboards);

        }
        public static void StartGetPlayerLeaderboardStandings(Combat newCombat)
        {
            lock (_updateLock)
            {
                var state = CombatLogStateBuilder.CurrentState;
                if (!state.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                    return;
                if (CurrentFightLeaderboard.Count == 0)
                    GetCurrentLeaderboard(newCombat);

                var returnData = new Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>>();
                var bossName = newCombat.EncounterBossInfo;
                var localPlayerClass = state.GetCharacterClassAtTime(newCombat.LocalPlayer, newCombat.StartTime);
                var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
                foreach (var participant in newCombat.CharacterParticipants)
                {
                    if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                    {
                        var participantClass = state.GetCharacterClassAtTime(participant, newCombat.StartTime);
                        var participantClassInfo = participantClass == null ? "Unknown" : participantClass.Name + "/" + participantClass.Discipline;
                        if (participantClassInfo != className)
                        {
                            returnData[participant] = null;
                            continue;
                        }
                    }
                    returnData[participant] = new Dictionary<LeaderboardEntryType, (double, bool)>();


                    foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
                    {
                        var parses = CurrentFightLeaderboard[enumVal];
                        if (!parses.Any(p => p.Character == participant.Name))
                            returnData[participant][enumVal] = (0,false);
                        else
                        {
                            var currentValue = GetValueForLeaderboardEntry(enumVal, newCombat, participant);
                            var currentMaxForParticipant = parses.Where(p => p.Character == participant.Name).MaxBy(v => v.Value).First();
                            var parsesWithVal = parses.Select(v => v.Value).ToList();
                            parsesWithVal.Add(currentValue);
                            returnData[participant][enumVal] = (parsesWithVal.OrderByDescending(v => v).ToList().IndexOf(currentValue) + 1,currentValue>=currentMaxForParticipant.Value);
                        }
                    }
                }
                LeaderboardStandingsAvailable(returnData);
            }
        }
        private static void GetCurrentLeaderboard(Combat newCombat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var bossName = newCombat.EncounterBossInfo;
            var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetCharacterClassAtTime(newCombat.LocalPlayer, newCombat.StartTime);
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                    CurrentFightLeaderboard[enumVal] = PostgresConnection.GetEntriesForBossWithClass(bossName, encounterName, className, enumVal);
                else
                    CurrentFightLeaderboard[enumVal] = PostgresConnection.GetEntriesForBossOfType(bossName, encounterName, enumVal);

            }
        }
        public static void Reset()
        {
            lock (_updateLock)
            {
                TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
                CurrentFightLeaderboard = new Dictionary<LeaderboardEntryType, List<LeaderboardEntry>>();
            }
        }

        public static void TryAddLeaderboardEntry(Combat combat)
        {
            //// REMOVE WITH 7.0
            //if (combat.WasPlayerKilled(combat.LocalPlayer))
            //    return;
            var state = CombatLogStateBuilder.CurrentState;
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                //// ADD WITH 7.0
                foreach (var player in combat.CharacterParticipants)
                {
                    //var player = combat.LocalPlayer;
                    SWTORClass playerClass;
                    if (!CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(player))
                    {
                        playerClass = null;
                    }
                    else
                    {
                        playerClass = state.GetCharacterClassAtTime(player, combat.StartTime);
                    }

                    var leaderboardEntry = new LeaderboardEntry()
                    {
                        Encounter = $"{combat.ParentEncounter.Name}",
                        Boss = combat.EncounterBossInfo,
                        Character = player.Name,
                        Class = playerClass == null ? "Unknown" : playerClass.Name + "/" + playerClass.Discipline,
                        Value = GetValueForLeaderboardEntry(enumVal, combat, player),
                        Type = enumVal,
                        Duration = (int)combat.DurationSeconds,
                        Version = _leaderboardVersion,
                        VerifiedKill = combat.WasBossKilled,
                        Logs = JsonConvert.SerializeObject(combat.AllLogs)
                    };
                    if (leaderboardEntry.Duration > 250 || combat.EncounterBossInfo.Contains("Parsing") || combat.WasBossKilled || !combat.WasPlayerKilled(player))
                    {
                        PostgresConnection.TryAddLeaderboardEntry(leaderboardEntry);
                        CombatIdentifier.UpdateOverlays(combat);
                    }
                }
            }
        }

        private static double GetValueForLeaderboardEntry(LeaderboardEntryType role, Combat combat, Entity player)
        {
            switch (role)
            {
                case LeaderboardEntryType.Damage:
                    return combat.DPS[player];
                case LeaderboardEntryType.FocusDPS:
                    return combat.FocusDPS[player];
                case LeaderboardEntryType.Healing:
                    return combat.HPS[player] + combat.PSPS[player];
                case LeaderboardEntryType.EffectiveHealing:
                    return combat.EHPS[player] + combat.PSPS[player];
                case LeaderboardEntryType.Mitigation:
                    return combat.MPS[player];
                default:
                    return 0;
            }
        }
    }

}
