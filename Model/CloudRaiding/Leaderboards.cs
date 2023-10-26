using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        private static object _getLock = new object();
        public static LeaderboardType CurrentLeaderboardType;
        public static event Action<Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>>> LeaderboardStandingsAvailable = delegate { };
        public static event Action<Dictionary<LeaderboardEntryType, (string, double)>> TopLeaderboardEntriesAvailable = delegate { };
        public static event Action<LeaderboardType> LeaderboardTypeChanged = delegate { };
        public static Dictionary<LeaderboardEntryType, (string, double)> TopLeaderboards = new Dictionary<LeaderboardEntryType, (string, double)>();
        public static Dictionary<LeaderboardEntryType, List<LeaderboardEntry>> CurrentFightLeaderboard = new Dictionary<LeaderboardEntryType, List<LeaderboardEntry>>();
        public static Combat CurrentCombat;
        private static List<string> healingDisciplines = new List<string> { "Corruption", "Medicine", "Bodyguard", "Seer", "Sawbones", "Combat Medic" };
        private static List<string> tankDisciplines = new List<string> { "Shield Tech", "Immortal", "Darkness", "Defense", "Shield Specialist", "Kinetic Combat" };
        private static double _maxParseValue = 500000;
        public static void Init()
        {
            _leaderboardVersion = PostgresConnection.GetCurrentLeaderboardVersion().ToString();
        }
        public static void UpdateLeaderboardType(LeaderboardType type)
        {
            LeaderboardSettings.SaveLeaderboardSettings(type);
            EncounterTimerTrigger.EncounterDetected += TrySetLeaderboardTops;
            CurrentLeaderboardType = type;
            CurrentFightLeaderboard.Clear();
            TopLeaderboards.Clear();
            LeaderboardTypeChanged(CurrentLeaderboardType);
            if (CurrentCombat == null)
                return;
            Task.Run(() =>
            {
                StartGetPlayerLeaderboardStandings(CurrentCombat);
                StartGetTopLeaderboardEntries(CurrentCombat);
            });

        }

        private static void TrySetLeaderboardTops(string arg1, string arg2, string arg3)
        {
            Task.Run(() =>
            {
                StartGetTopLeaderboardEntries(CombatIdentifier.CurrentCombat);
            });
        }

        public static void UpdateOverlaysWithNewLeaderboard(Combat combat)
        {
            if (combat.IsCombatWithBoss)
            {
                Task.Run(() =>
                {
                    Reset();
                    StartGetTopLeaderboardEntries(combat);
                    StartGetPlayerLeaderboardStandings(combat);
                });
            }
        }
        public static void StartGetTopLeaderboardEntries(Combat newCombat)
        {
            lock (_getLock)
            {
                if (CurrentLeaderboardType == LeaderboardType.Off)
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

                if (newCombat.LocalPlayer == null ||
                    !CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                    return;
                if (CurrentFightLeaderboard.Count == 0)
                    GetCurrentLeaderboard(newCombat);
                var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
                var className = localPlayerClass == null
                    ? "Unknown"
                    : localPlayerClass.Name + "/" + localPlayerClass.Discipline;

                foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
                {
                    if (!CurrentFightLeaderboard.ContainsKey(enumVal))
                        continue;
                    LeaderboardEntry topParse;
                    if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                        topParse = CurrentFightLeaderboard[enumVal].Where(v => v.Class == className)
                            .MaxBy(v => v.Value);
                    else
                        topParse = CurrentFightLeaderboard[enumVal].MaxBy(v => v.Value);
                    if (topParse == null || string.IsNullOrEmpty(topParse.Character))
                        continue;
                    TopLeaderboards[enumVal] = (topParse.Character, topParse.Value);
                }
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
                if (newCombat.LocalPlayer == null || !state.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                    return;
                if (CurrentFightLeaderboard.Count == 0)
                    GetCurrentLeaderboard(newCombat);

                var returnData = new Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>>();
                var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
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
                        if (parses == null)
                            returnData[participant][enumVal] = (0, false);
                        else
                        {
                            var currentValue = GetValueForLeaderboardEntry(enumVal, newCombat, participant);
                            var parsesWithVal = parses.Where(p => MatchesRole(enumVal, p.Class)).Select(v => v.Value).ToList();
                            if (parses.All(p => p.Character != participant.Name))
                            {
                                //returnData[participant][enumVal] = (parsesWithVal.OrderByDescending(v => v).ToList().IndexOf(currentValue) + 1,false);
                                returnData[participant][enumVal] = (
                                    GetLeaderboardPercentile(parsesWithVal, currentValue), true);
                            }
                            else
                            {
                                var currentMaxForParticipant = parses.Where(p => p.Character == participant.Name).MaxBy(v => v.Value);
                                var fresh = parsesWithVal.RemoveAll(v => v.Equals(currentValue));
                                //returnData[participant][enumVal] = (parsesWithVal.OrderByDescending(v => v).ToList().IndexOf(currentValue) + 1,currentValue>=currentMaxForParticipant.Value);
                                returnData[participant][enumVal] = (GetLeaderboardPercentile(parsesWithVal, currentValue), currentValue >= currentMaxForParticipant.Value);
                            }

                        }
                    }
                }
                LeaderboardStandingsAvailable(returnData);
            }
        }

        private static bool MatchesRole(LeaderboardEntryType enumVal, string argClass)
        {
            var discipline = argClass.Split('/').Last();
            var role = healingDisciplines.Contains(discipline) ? "Healer" :
                tankDisciplines.Contains(discipline) ? "Tank" : "DPS";
            if (enumVal == LeaderboardEntryType.Damage || enumVal == LeaderboardEntryType.FocusDPS)
            {
                return role == "DPS";
            }

            if (enumVal == LeaderboardEntryType.Healing || enumVal == LeaderboardEntryType.EffectiveHealing)
            {
                return role == "Healer";
            }

            if (enumVal == LeaderboardEntryType.Mitigation)
            {
                return role == "Tank";
            }

            return false;
        }

        private static void GetCurrentLeaderboard(Combat newCombat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var bossName = newCombat.EncounterBossInfo;
            var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
            var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                {
                    var results = PostgresConnection.GetEntriesForBossOfType(bossName, encounterName, enumVal).Result;
                    if (bossName.Contains("4 "))
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
            bool updatedAny = false;
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
                    if (leaderboardEntry.Value < _maxParseValue)
                    {
                        if (CheckForValidParseUpload(combat) || CheckForValidCombatUpload(combat, player))
                        {
                            updatedAny = await PostgresConnection.TryAddLeaderboardEntry(leaderboardEntry);
                        }
                    }
                }
            }
            if (updatedAny)
                UpdateOverlaysWithNewLeaderboard(combat);
        }
        private static bool CheckForValidCombatUpload(Combat combat, Entity player)
        {
            if (combat.DurationSeconds < 5)
                return false;
            if (combat.ParentEncounter.Name == "Parsing")
                return false;
            if (combat.WasBossKilled)
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

        private static double GetLeaderboardPercentile(List<double> entries, double currentValue)
        {
            if (entries.Count == 0) return 0;
            return Math.Floor(((entries.Count(v => v < currentValue) + (0.5 * entries.Count(v => v.Equals(currentValue)))) / (double)entries.Count) * 100d);
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
