using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Avalonia_TEMP;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public enum LeaderboardType
    {
        Off,
        AllDiciplines,
        LocalDicipline,
        LocalRole
    }
    public class StandingsUpdateInfo
    {
        public DateTime UpdateTime { get; set; } 
        public double Value { get; set; }
    }
    public static class Leaderboards
    {
        public static string _leaderboardVersion = "3";
        public static object _updateLock = new object();
        private static object _getLock = new object();
        public static LeaderboardType CurrentLeaderboardType;
        public static event Action<Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>>> LeaderboardStandingsAvailable = delegate { };
        public static event Action<Dictionary<LeaderboardEntryType, (string, double)>> TopLeaderboardEntriesAvailable = delegate { };
        public static event Action<LeaderboardType> LeaderboardTypeChanged = delegate { };

        public static ConcurrentDictionary<LeaderboardEntryType, int[]> LeaderboardPercentiles = new ConcurrentDictionary<LeaderboardEntryType, int[]>();
        public static ConcurrentDictionary<LeaderboardEntryType, (string, double)> TopLeaderboards = new ConcurrentDictionary<LeaderboardEntryType, (string, double)>();
        public static Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>> LeaderboardStandings = new Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>>();
        public static Combat CurrentCombat;

        private static double _maxParseValue = 500000;
        public static async void Init()
        {
            var lbVersion = await API_Connection.GetCurrentLeaderboardVersion();
            _leaderboardVersion = lbVersion.ToString();
        }
        public static void UpdateLeaderboardType(LeaderboardType type)
        {
            LeaderboardSettings.SaveLeaderboardSettings(type);
            CurrentLeaderboardType = type;
            TopLeaderboards.Clear();
            LeaderboardPercentiles.Clear();
            LeaderboardTypeChanged.InvokeSafely(CurrentLeaderboardType);
            if (CurrentCombat == null)
                return;
            Task.Run(() =>
            {
                StartGetPlayerLeaderboardStandings(CurrentCombat);
                StartGetTopLeaderboardEntries(CurrentCombat);
            });

        }

        public static void UpdateOverlaysWithNewLeaderboard(Combat combat, bool reloadLb)
        {
            if (combat.IsCombatWithBoss)
            {
                Task.Run(() =>
                {
                    if(reloadLb)
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
                    TopLeaderboards = new ConcurrentDictionary<LeaderboardEntryType, (string, double)>();
                    TopLeaderboardEntriesAvailable.InvokeSafely(TopLeaderboards.ToDictionary());
                    return;
                }

                var state = CombatLogStateBuilder.CurrentState;
                CurrentCombat = newCombat;
                if (TopLeaderboards.Count > 0)
                {
                    TopLeaderboardEntriesAvailable.InvokeSafely(TopLeaderboards.ToDictionary());
                    return;
                }

                if (newCombat.LocalPlayer == null ||
                    !CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                    return;

                Parallel.ForEach(Enum.GetValues(typeof(LeaderboardEntryType)).Cast<LeaderboardEntryType>(), enumVal =>
                {
                    //TODO add in an API call to get top parse values
                    var topInfo = GetTop(newCombat, enumVal, CurrentLeaderboardType == LeaderboardType.LocalDicipline);
                    TopLeaderboards[enumVal] = (topInfo.Name, topInfo.Value);
                });
            }

            TopLeaderboardEntriesAvailable.InvokeSafely(TopLeaderboards.ToDictionary());

        }
        private static int percentileUpdates = 0;
        public static void StartGetPlayerLeaderboardStandings(Combat newCombat)
        {
            lock (_updateLock)
            {
                LeaderboardStandings = new Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>>();
                if (CurrentLeaderboardType == LeaderboardType.Off)
                {
                    LeaderboardStandingsAvailable.InvokeSafely(LeaderboardStandings);
                    return;
                }
                var state = CombatLogStateBuilder.CurrentState;
                if (newCombat.LocalPlayer == null || !state.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                    return;
                if (LeaderboardPercentiles.IsEmpty)
                {
                    SetPercentiles(newCombat);
                }
                var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
                var className = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
                foreach (var participant in newCombat.CharacterParticipants)
                {
                    if (!LeaderboardStandings.ContainsKey(participant))
                    {
                        LeaderboardStandings[participant] = new ConcurrentDictionary<LeaderboardEntryType, (double, bool)>();
                    }
                    var participantClass = state.GetCharacterClassAtTime(participant, newCombat.StartTime);
                    var participantClassInfo = participantClass == null ? "Unknown" : participantClass.Name + "/" + participantClass.Discipline;
                    if (CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                    {
                        if (participantClassInfo != className)
                        {
                            LeaderboardStandings[participant] = null;
                            continue;
                        }
                    }
                    if (CurrentLeaderboardType == LeaderboardType.LocalRole)
                    {
                        if (participantClass == null || localPlayerClass == null || participantClass.Role != localPlayerClass.Role)
                        {
                            LeaderboardStandings[participant] = null;
                            continue;
                        }
                    }
                    Parallel.ForEach(Enum.GetValues(typeof(LeaderboardEntryType)).Cast<LeaderboardEntryType>(), enumVal =>
                    {
                        var currentValue = GetValueForLeaderboardEntry(enumVal, newCombat, participant);
                        var percentiles = LeaderboardPercentiles[enumVal].ToList();
                        var percentileForValue = 0;
                        if (percentiles[0] > currentValue || percentiles[99] <= currentValue)
                        {
                            percentileForValue = percentiles[99] <= currentValue ? 100 : 0;
                        }
                        else
                        {
                            percentileForValue = percentiles[0] > currentValue ? 0 : percentiles.IndexOf(percentiles.First(p => p >= currentValue))+1;
                        }
                         

                        LeaderboardStandings[participant][enumVal] =(percentileForValue, false);            
                    });
                }
                LeaderboardStandingsAvailable.InvokeSafely(LeaderboardStandings);
            }
        }
        private static LeaderboardTop GetTop(Combat newCombat, LeaderboardEntryType type, bool useClass = false)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var bossName = newCombat.EncounterBossInfo;
            if (string.IsNullOrEmpty(bossName))
                return new LeaderboardTop();
            var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
            var playerClass = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            return API_Connection.GetTopBossEntry(bossName, encounterName, type, playerClass, useClass).Result;
        }
        private static void SetPercentiles(Combat newCombat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var bossName = newCombat.EncounterBossInfo;
            if (string.IsNullOrEmpty(bossName))
                return;
            var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
            var playerClass = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            Parallel.ForEach(Enum.GetValues(typeof(LeaderboardEntryType)).Cast<LeaderboardEntryType>(), type =>
            {
                if(CurrentLeaderboardType == LeaderboardType.AllDiciplines || localPlayerClass == null)
                    LeaderboardPercentiles[type] = API_Connection.GetLeaderboardPercentiles(bossName, encounterName, type).Result;
                if(CurrentLeaderboardType == LeaderboardType.LocalRole)
                    LeaderboardPercentiles[type] = API_Connection.GetLeaderboardPercentilesForRole(bossName, encounterName, type, localPlayerClass.Role.ToString()).Result;
                if(CurrentLeaderboardType == LeaderboardType.LocalDicipline)
                    LeaderboardPercentiles[type] = API_Connection.GetLeaderboardPercentilesForDiscipline(bossName, encounterName, type, playerClass).Result;
            });
        }
        public static void Reset()
        {
            lock (_updateLock)
            {
                LeaderboardPercentiles = new ConcurrentDictionary<LeaderboardEntryType, int[]>();
                TopLeaderboards = new ConcurrentDictionary<LeaderboardEntryType, (string, double)>();
                LeaderboardStandings = new Dictionary<Entity,ConcurrentDictionary<LeaderboardEntryType,(double, bool)>>();
            }
        }

        public static async void TryAddLeaderboardEntry(Combat combat)
        {
            var state = CombatLogStateBuilder.CurrentState;
            List<LeaderboardEntry> boardEntries = new List<LeaderboardEntry>();
            foreach (LeaderboardEntryType enumVal in Enum.GetValues(typeof(LeaderboardEntryType)))
            {
                foreach (var player in combat.CharacterParticipants)
                {
                    var newValue = GetValueForLeaderboardEntry(enumVal, combat, player);

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
                        Value = newValue,
                        Type = enumVal,
                        Duration = combat.DurationSeconds,
                        Version = _leaderboardVersion,
                        VerifiedKill = combat.WasBossKilled,
                        TimeStamp = combat.EndTime,
                    };
                    if (leaderboardEntry.Value < _maxParseValue)
                    {
                        if (CheckForValidParseUpload(combat) || CheckForValidCombatUpload(combat, player))
                        {
                            boardEntries.Add(leaderboardEntry);
                        }
                    }
                }
            }
            //todo refactor this to be more efficient when the avalonia code is integrated
            if(combat.WasBossKilled && !combat.ParentEncounter.IsOpenWorld)
                AvaloniaTimelineBuilder.UploadBossKill(combat.EncounterBossDifficultyParts.Item1, combat.ParentEncounter.Name, combat.ParentEncounter.Difficutly, combat.ParentEncounter.NumberOfPlayer, combat.StartTime, combat.EndTime);
            
            bool updatedAny = boardEntries.Count == 0 ? false : await API_Connection.TryAddLeaderboardEntries(boardEntries);
            UpdateOverlaysWithNewLeaderboard(combat, updatedAny);
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
    public class PercentileInfo
    {
        public int Percentile { get; set; }
        public bool IsPersonalBest { get; set; }
    }
    public class LeaderboardTop
    {
        public double Value { get; set; }
        public string Name { get; set; }
    }
}
