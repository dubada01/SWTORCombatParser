using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CombatParsing;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public enum LeaderboardType
    {
        Off,
        AllDiciplines,
        LocalDicipline
    }
    public static class Leaderboards
    {
        public static string _leaderboardVersion = "2";
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
            LeaderboardSettings.SaveLeaderboardSettings(type);
            CurrentLeaderboardType = type;
            CurrentFightLeaderboard.Clear();
            TopLeaderboards.Clear();
            LeaderboardTypeChanged(CurrentLeaderboardType);
            if (CurrentCombat == null)
                return;
            Task.Run(() => { 
                StartGetPlayerLeaderboardStandings(CurrentCombat);
                StartGetTopLeaderboardEntries(CurrentCombat);
            });
            
        }
        public static void StartGetTopLeaderboardEntries(Combat newCombat)
        {
            if(CurrentLeaderboardType == LeaderboardType.Off)
            {
                TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
                TopLeaderboardEntriesAvailable(TopLeaderboards);
                return;
            }
            var state = CombatLogStateBuilder.CurrentState;
            CurrentCombat = newCombat;
            if (TopLeaderboards.Count > 0)
            { 
                TopLeaderboardEntriesAvailable(TopLeaderboards);
                return;
            }
            if (!CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                return;
            if (CurrentFightLeaderboard.Count == 0)
                GetCurrentLeaderboard(newCombat);
            //var bossName = newCombat.EncounterBossInfo;
            //var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetCharacterClassAtTime(newCombat.LocalPlayer,newCombat.StartTime);
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;

            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                if (!CurrentFightLeaderboard.ContainsKey(enumVal))
                    continue;
                LeaderboardEntry topParse;
                if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                    topParse = CurrentFightLeaderboard[enumVal].Where(v=>v.Class == className).MaxBy(v=>v.Value);
                else
                    topParse = CurrentFightLeaderboard[enumVal].MaxBy(v => v.Value);
                if (topParse == null || string.IsNullOrEmpty(topParse.Character))
                    continue;
                TopLeaderboards[enumVal] = (topParse.Character, topParse.Value);
            }
            TopLeaderboardEntriesAvailable(TopLeaderboards);

        }
        public static void StartGetPlayerLeaderboardStandings(Combat newCombat)
        {
            lock (_updateLock)
            {
                if (CurrentLeaderboardType == LeaderboardType.Off)
                {
                    CurrentFightLeaderboard = new Dictionary<LeaderboardEntryType, List<LeaderboardEntry>>();
                    LeaderboardStandingsAvailable(new Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>>());
                    return;
                }
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
                        if (!CurrentFightLeaderboard.ContainsKey(enumVal))
                            continue;
                        var parses = CurrentFightLeaderboard[enumVal];
                        if (parses == null || !parses.Any(p => p.Character == participant.Name))
                            returnData[participant][enumVal] = (0,false);
                        else
                        {
                            var currentValue = GetValueForLeaderboardEntry(enumVal, newCombat, participant);
                            var currentMaxForParticipant = parses.Where(p => p.Character == participant.Name).MaxBy(v => v.Value);
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
                {
                    var results = PostgresConnection.GetEntriesForBossOfType(bossName, encounterName, enumVal).Result;
                    if(bossName.Contains("4 "))
                    {
                        var oldFPResults = PostgresConnection.GetEntriesForBossOfType(newCombat.OldFlashpointBossInfo, encounterName, enumVal).Result;
                        results.AddRange(oldFPResults);
                    }
                    CurrentFightLeaderboard[enumVal] = results.Where(r => r.Class == className).ToList();
                }
                else
                {
                    var allResults = PostgresConnection.GetEntriesForBossOfType(bossName, encounterName, enumVal).Result;
                    if (bossName.Contains("4 "))
                    {
                        var oldFPResults = PostgresConnection.GetEntriesForBossOfType(newCombat.OldFlashpointBossInfo, encounterName, enumVal).Result;
                        allResults.AddRange(oldFPResults);
                    }
                    CurrentFightLeaderboard[enumVal] = allResults;
                }
                    

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

        public static async void TryAddLeaderboardEntry(Combat combat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                foreach (var player in combat.CharacterParticipants)
                {
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
                        TimeStamp = combat.EndTime,
                    };
                    if (CheckForValidParseUpload(combat)||CheckForValidCombatUpload(combat,player))
                    {
                        await PostgresConnection.TryAddLeaderboardEntry(leaderboardEntry);
                    }
                }
            }
            CombatIdentifier.FinalizeOverlay(combat);
        }
        private static bool CheckForValidCombatUpload(Combat combat, Entity player)
        {
            if (combat.ParentEncounter.Name == "Parsing")
                return false;
            if (combat.WasBossKilled)
                return true;
            if (combat.DurationSeconds < 250)
                return false;
            if (!combat.WasPlayerKilled(player))
                return true;
            return false;
        }
        private static bool CheckForValidParseUpload(Combat combat)
        {
            if (combat.ParentEncounter.Name != "Parsing")
                return false;
            if (!combat.WasBossKilled)
                return false;
            return true;
        }
        private static double GetValueForLeaderboardEntry(LeaderboardEntryType role, Combat combat, Entity player)
        {
            switch (role)
            {
                case LeaderboardEntryType.Damage:
                    return combat.EDPS[player];
                case LeaderboardEntryType.FocusDPS:
                    return combat.EFocusDPS[player];
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
