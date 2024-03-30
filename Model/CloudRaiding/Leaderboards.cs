using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Leaderboard;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public enum LeaderboardType
    {
        Off,
        AllDiciplines,
        LocalDicipline
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

        public static ConcurrentDictionary<LeaderboardEntryType, (string, double)> TopLeaderboards = new ConcurrentDictionary<LeaderboardEntryType, (string, double)>();
        public static Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>> LeaderboardStandings = new Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>>();
        public static ConcurrentDictionary<Entity, ConcurrentDictionary<LeaderboardEntryType,StandingsUpdateInfo>> LeaderboardStandingsUpdateInfo = new ConcurrentDictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, StandingsUpdateInfo>>();
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
            LeaderboardTypeChanged(CurrentLeaderboardType);
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
                    TopLeaderboardEntriesAvailable(TopLeaderboards.ToDictionary());
                    return;
                }

                var state = CombatLogStateBuilder.CurrentState;
                CurrentCombat = newCombat;
                if (TopLeaderboards.Count > 0)
                {
                    TopLeaderboardEntriesAvailable(TopLeaderboards.ToDictionary());
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

            TopLeaderboardEntriesAvailable(TopLeaderboards.ToDictionary());

        }
        private static int percentileUpdates = 0;
        public static void StartGetPlayerLeaderboardStandings(Combat newCombat)
        {
            lock (_updateLock)
            {
                if (CurrentLeaderboardType == LeaderboardType.Off)
                {
                    LeaderboardStandings = new Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>>();
                    LeaderboardStandingsAvailable(LeaderboardStandings);
                    return;
                }
                var state = CombatLogStateBuilder.CurrentState;
                if (newCombat.LocalPlayer == null || !state.PlayerClassChangeInfo.ContainsKey(newCombat.LocalPlayer))
                    return;
                
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
                    Parallel.ForEach(Enum.GetValues(typeof(LeaderboardEntryType)).Cast<LeaderboardEntryType>(), enumVal =>
                    {
                        var currentValue = GetValueForLeaderboardEntry(enumVal, newCombat, participant);
                        if (!NeedsUpdate(enumVal, participant, currentValue))
                            return;
                        var pecentileInfo = GetPercentile(newCombat, participant.Name, enumVal, currentValue, participantClassInfo, CurrentLeaderboardType == LeaderboardType.LocalDicipline);
                        //Debug.WriteLine("Updated Percentiles: " + (DateTime.Now - startTime).TotalMilliseconds);
                        LeaderboardStandings[participant][enumVal] =(pecentileInfo.Percentile, pecentileInfo.IsPersonalBest);            
                    });
                }
                LeaderboardStandingsAvailable(LeaderboardStandings);
            }
        }

        private static bool NeedsUpdate(LeaderboardEntryType type, Entity participant, double value)
        {
            double minSecondsBetween = 10;
            double percentRequiredForUpdate = 10;
            if (!LeaderboardStandingsUpdateInfo.ContainsKey(participant))
            {
                LeaderboardStandingsUpdateInfo[participant] = new ConcurrentDictionary<LeaderboardEntryType, StandingsUpdateInfo>();
                return true;
            }
            if (!LeaderboardStandingsUpdateInfo[participant].ContainsKey(type))
            {
                LeaderboardStandingsUpdateInfo[participant][type] = new StandingsUpdateInfo { UpdateTime = DateTime.Now, Value = value };
                return true;
            }
            var info = LeaderboardStandingsUpdateInfo[participant][type];
            if(((Math.Abs(value - info.Value)/info.Value) * 100) >= percentRequiredForUpdate && (DateTime.Now - info.UpdateTime).TotalSeconds > minSecondsBetween)
            {
                LeaderboardStandingsUpdateInfo[participant][type] = new StandingsUpdateInfo { UpdateTime = DateTime.Now, Value = value };
                return true;
            }
            return false;
        }

        private static PercentileInfo GetPercentile(Combat newCombat, string playerName, LeaderboardEntryType type, double value, string parcitipantClass, bool useClass = false)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var bossName = newCombat.EncounterBossInfo;
            if (string.IsNullOrEmpty(bossName))
                return new PercentileInfo();
            var encounterName = newCombat.ParentEncounter.Name;
            var localPlayerClass = state.GetLocalPlayerClassAtTime(newCombat.StartTime);
            var playerClass = localPlayerClass == null ? "Unknown" : localPlayerClass.Name + "/" + localPlayerClass.Discipline;
            return API_Connection.GetPercentileForBoss(bossName, encounterName, type, playerName, playerClass, value, parcitipantClass, useClass).Result;
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
        public static void Reset()
        {
            lock (_updateLock)
            {
                LeaderboardStandingsUpdateInfo = new ConcurrentDictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, StandingsUpdateInfo>>();
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
