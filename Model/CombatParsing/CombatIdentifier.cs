using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatIdentifier
    {
        public static event Action<Combat> CombatFinished = delegate { };
        public static Combat CurrentCombat { get; set; }

        private static object _modlock = new object();
        private static bool _initialized;
        /// <summary>
        /// Call once to reset the incremental generator.
        /// </summary>
        public static void Initialize()
        {
            lock (_modlock)
            {
                CurrentCombat = new Combat();
                _initialized = true;
            }
        } 
        /// <summary>
        /// Merge incomingLogs into the existing CurrentCombat, appending only new entries.
        /// </summary>
        public static Combat UpdateCurrentCombatFromLogs(
            List<ParsedLogEntry> ongoingLogs,
            bool isRealtime = false,
            bool quietOverlays = false,
            bool combatEndUpdate = false,
            bool isPhaseCombat = false)
        {
            if (ongoingLogs == null || ongoingLogs.Count == 0)
                return CurrentCombat;

            lock (_modlock)
            {
                if (!_initialized)
                    Initialize();

                // Sort the incoming batch by timestamp
                var orderedIncoming = ongoingLogs.OrderBy(l => l.TimeStamp).ToList();

                // Identify new logs not already in CurrentCombat.AllLogs
                var newLogs = orderedIncoming
                    .Where(l => !CurrentCombat.AllLogs.Contains(l))
                    .ToList();

                if (newLogs.Count == 0)
                    return CurrentCombat;

                // Merge each new log
                foreach (var log in newLogs)
                {
                    // 1) HashSet of all logs
                    CurrentCombat.AllLogs.Add(log);

                    // 2) Per-entity logs
                    MergeEntityLog(log.Source, log);
                    MergeEntityLog(log.Target, log);

                    // 3) Participants and classes
                    if (log.Source.IsCharacter || log.Source.IsCompanion)
                        AddParticipant(log.Source, log.TimeStamp);
                    if (log.Target.IsCharacter || log.Target.IsCompanion)
                        AddParticipant(log.Target, log.TimeStamp);

                    // 4) Targets
                    if (!log.Target.IsCharacter && !log.Target.IsCompanion)
                        AddTarget(log.Target);

                    // 5) Start and End times
                    if (CurrentCombat.StartTime == default)
                    {
                        CurrentCombat.StartTime = log.TimeStamp;
                    }
                    CurrentCombat.EndTime = log.TimeStamp;
                }

                // 6) Post-merge metadata (threat, shielding, cooldowns)
                CombatMetaDataParse.PopulateMetaData(CurrentCombat);

                var absorbLogs = CurrentCombat.IncomingDamageMitigatedLogs
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value
                            .Where(e => e.Value.Modifier.ValueType == DamageType.absorbed)
                            .OrderBy(e => e.TimeStamp)
                            .ToList()
                    );
                AddShieldingToLogs.AddShieldLogsByTarget(absorbLogs, CurrentCombat);
                AddTankCooldown.AddDamageSavedDuringCooldown(CurrentCombat);

                // 7) Real-time triggers
                if (CurrentCombat.IsCombatWithBoss && isRealtime)
                {
                    var parts = CurrentCombat.EncounterBossDifficultyParts;
                    EncounterTimerTrigger.FireEncounterDetected(
                        CurrentCombat.ParentEncounter.Name,
                        parts.Item1,
                        CurrentCombat.ParentEncounter.Difficutly
                    );
                }

                // 8) Combat finished event
                if (combatEndUpdate)
                    CombatFinished.InvokeSafely(CurrentCombat);

                return CurrentCombat;
            }
        }

        private static void MergeEntityLog(Entity e, ParsedLogEntry log)
        {
            if (!CurrentCombat.LogsInvolvingEntity.TryGetValue(e, out var list))
            {
                list = new List<ParsedLogEntry>();
                CurrentCombat.LogsInvolvingEntity[e] = list;
            }
            list.Add(log);
        }

        private static void AddParticipant(Entity e, DateTime timestamp)
        {
            if (!CurrentCombat.CharacterParticipants.Any(p => p.LogId == e.LogId))
            {
                CurrentCombat.CharacterParticipants.Add(e);
                var cls = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(e, timestamp);
                CurrentCombat.CharacterClases[e] = cls;
            }
        }

        private static void AddTarget(Entity e)
        {
            if (!CurrentCombat.Targets.Any(t => t.LogId == e.LogId))
                CurrentCombat.Targets.Add(e);
        }
        public static Combat GenerateNewCombatFromLogs(List<ParsedLogEntry> ongoingLogs, bool isRealtime = false, bool quietOverlays = false, bool combatEndUpdate = false, bool isPhaseCombat = false)
        {
            try
            {
                if (ongoingLogs.Count == 0)
                    return new Combat();
                var state = CombatLogStateBuilder.CurrentState;
                var orderedLogs = ongoingLogs.OrderBy(t => t.TimeStamp);
                var firstTime = orderedLogs.First().TimeStamp;
                var encounter = GetEncounterInfo(firstTime);
                var currentPariticpants = orderedLogs.Where(l => l.Source.IsCharacter || l.Source.IsCompanion)
                    .Where(l => l.Effect.EffectType != EffectType.TargetChanged).Select(p => p.Source).Distinct()
                    .ToList();

                currentPariticpants.AddRange(orderedLogs.Where(l => l.Target.IsCharacter || l.Target.IsCompanion)
                    .Where(l => l.Effect.EffectType != EffectType.TargetChanged).Select(p => p.Target).Distinct()
                    .ToList());
                var participants = currentPariticpants.GroupBy(p => p.Id).Select(x => x.FirstOrDefault()).ToList();
                var participantInfos = orderedLogs.Select(p => p.SourceInfo).Distinct().ToList();
                var classes = participantInfos.GroupBy(p => p.Entity.Id).Select(x => x.FirstOrDefault())
                    .ToDictionary(k => k.Entity, k => state.GetCharacterClassAtTime(k.Entity, firstTime));

                var orderedLogsList = orderedLogs.ToHashSet();
                var targets = GetTargets(orderedLogsList);
                var allEntities = new List<Entity>().Concat(targets).Concat(currentPariticpants).Distinct().ToList();

                // Create the dictionary
                Dictionary<Entity, List<ParsedLogEntry>> entityLogs = allEntities
                    .ToDictionary(
                        p => p,
                        p => ongoingLogs.AsParallel().WithDegreeOfParallelism(8)
                            .Where(l => p.LogId == l.Target.LogId || p.LogId == l.Source.LogId)
                            .OrderBy(t => t.TimeStamp)
                            .ToList()
                    );

                var newCombat = new Combat()
                {
                    CharacterParticipants = participants,
                    CharacterClases = classes,
                    StartTime = orderedLogs.First().TimeStamp,
                    EndTime = orderedLogs.Last().TimeStamp,
                    Targets = targets,
                    AllLogs = orderedLogsList,
                    LogsInvolvingEntity = entityLogs
                };
                if (encounter != null && encounter.BossInfos != null)
                {
                    newCombat.ParentEncounter = encounter;
                    newCombat.EncounterBossDifficultyParts = GetCurrentBossInfo(ongoingLogs, encounter);
                    newCombat.BossInfo = GetCurrentBossInfoObject(ongoingLogs, encounter);
                    UpdateBossEntities(ongoingLogs, encounter);
                    //newCombat.RequiredDeadTargetsForKill = GetTargetsRequiredForKill(ongoingLogs, encounter);
                }

                if (newCombat.IsCombatWithBoss)
                {
                    var parts = newCombat.EncounterBossDifficultyParts;
                    if (isRealtime)
                        EncounterTimerTrigger.FireEncounterDetected(newCombat.ParentEncounter.Name, parts.Item1,
                            newCombat.ParentEncounter.Difficutly);
                }

                if (newCombat.Targets.Any(t => t.LogId == 2857785339412480))
                {
                    newCombat.ParentEncounter = new EncounterInfo()
                    {
                        Name = "Parsing",
                        LogName = "Parsing",
                        Difficutly = "Parsing",
                        NumberOfPlayer = "1",
                        EncounterType = EncounterType.Parsing,
                        BossIds = new Dictionary<string, Dictionary<string, List<long>>>()
                        {
                            {
                                "Training Dummy",
                                new Dictionary<string, List<long>>()
                                    { { "Parsing 1", new List<long> { 2857785339412480 } } }
                            }
                        },
                        RequiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>()
                        {
                            {
                                "Training Dummy",
                                new Dictionary<string, List<long>>()
                                    { { "Parsing 1", new List<long> { 2857785339412480 } } }
                            }
                        },
                    };
                    newCombat.EncounterBossDifficultyParts = GetCurrentBossInfo(ongoingLogs, encounter);
                    newCombat.BossInfo = GetCurrentBossInfoObject(ongoingLogs, encounter);
                }

                lock (_modlock)
                {
                    CombatMetaDataParse.PopulateMetaData(newCombat);
                    //Debug.WriteLine("###############"+JsonConvert.SerializeObject(newCombat.PlayerThreatPerEnemy.ToDictionary(kvp=>kvp.Key.Name + kvp.Key.Id, kvp=>kvp.Value.ToDictionary(a=>a.Key.Name + kvp.Key.Id, a=>a.Value)), Formatting.Indented)+"-------------------\r\n");
                    
                    var absorbLogs = newCombat.IncomingDamageMitigatedLogs.ToDictionary(kvp => kvp.Key,
                        kvp => kvp.Value.AsParallel().WithDegreeOfParallelism(8)
                            .Where(l => l.Value.Modifier.ValueType == DamageType.absorbed).OrderBy(l => l.TimeStamp)
                            .ToList());
                    AddShieldingToLogs.AddShieldLogsByTarget(absorbLogs, newCombat);
                    AddTankCooldown.AddDamageSavedDuringCooldown(newCombat);
                }

                if (combatEndUpdate)
                {
                    CombatFinished.InvokeSafely(newCombat);
                }

                return newCombat;
            }
            catch(Exception e)
            {
                Logging.LogError("Failed to create combat: " + e.Message + "\r\n" + e.StackTrace);
                return new Combat();
            }
        }

        private static List<Entity> GetTargets(HashSet<ParsedLogEntry> logs)
        {
            var combatStart = logs.First().TimeStamp;
            HashSet<Entity> targets = new HashSet<Entity>();

            foreach (var log in logs)
            {
                if (log.Effect.EffectType != EffectType.TargetChanged && log.Effect.EffectId == _7_0LogParsing._damageEffectId)
                {
                    if (log.Target != null && (!log.Target.IsCharacter || CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(log.Target, combatStart)) && !log.Target.IsCompanion && log.Target.Name != null)
                    {
                        targets.Add(log.Target);
                    }

                    if (log.Source != null && (!log.Source.IsCharacter || CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(log.Source, combatStart)) && !log.Source.IsCompanion && log.Source.Name != null)
                    {
                        targets.Add(log.Source);
                    }
                }
            }

            return targets.ToList();
        }
        private static EncounterInfo GetEncounterInfo(DateTime combatStartTime)
        {
            return CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(combatStartTime);
        }
        public static (string, string, string) GetCurrentBossInfo(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null)
                return ("", "", "");

            var validLogs = logs.Where(l => !(l.Effect.EffectType == EffectType.TargetChanged && l.Source.IsCharacter) && !string.IsNullOrEmpty(l.Target.Name)).ToList();
            if (currentEncounter.Name.Contains("Open World"))
            {
                if (validLogs.Select(l => l.Target).DistinctBy(t => t.LogId).Any(t => EncounterLoader.OpenWorldBosses.Any(owb=>owb.BossId == t.LogId)))
                {
                    var dummyTarget = validLogs.Select(l => l.TargetInfo).First(t => EncounterLoader.OpenWorldBosses.Any(owb => owb.BossId == t.Entity.LogId));
                    var owb = EncounterLoader.OpenWorldBosses.First(owb => owb.BossId == dummyTarget.Entity.LogId);
                    if (owb.BossName == "Training Dummy")
                    {
                        var dummyMaxHP = dummyTarget.MaxHP;
                        currentEncounter.Difficutly = dummyMaxHP.ToString();
                        return (dummyTarget.Entity.Name, dummyMaxHP + "HP", "");
                    }
                    else
                    {
                        return (owb.BossName, "Open World", "");
                    }
                }
                else
                {
                    return ("", "", "");
                }
            }
            var bossesDetected = GetCurrentBossNames(validLogs, currentEncounter);
            if (bossesDetected.Count == 0)
                return ("", "", "");
            var boss = currentEncounter.BossInfos.FirstOrDefault(b => bossesDetected.All(t => b.TargetIds.Contains(t)));
            if (boss != null)
            {
                return (boss.EncounterName, currentEncounter.NumberOfPlayer.Replace("Player", "").Trim(), currentEncounter.Difficutly);
            }

            return ("", "", "");
        }
        public static BossInfo GetCurrentBossInfoObject(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null)
                return new BossInfo();

            var validLogs = logs.Where(l => !(l.Effect.EffectType == EffectType.TargetChanged && l.Source.IsCharacter) && !string.IsNullOrEmpty(l.Target.Name)).ToList();

            var bossesDetected = GetCurrentBossNames(validLogs, currentEncounter);
            if (bossesDetected.Count == 0)
                return new BossInfo();
            var boss = currentEncounter.BossInfos.FirstOrDefault(b => bossesDetected.All(t => b.TargetIds.Contains(t)));
            if (boss != null)
            {
                return boss;
            }

            return new BossInfo();
        }
        private static List<string> GetCurrentBossNames(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null)
                return new List<string>();

            var bossIds = new HashSet<string>(currentEncounter.BossInfos.SelectMany(b => b.TargetIds));

            var bossNamesFound = new List<string>();
            foreach (var log in logs)
            {
                if (bossIds.Contains(log.Source.LogId.ToString()))
                {
                    if (log.Effect.EffectType == EffectType.Remove)
                    {
                        continue;
                    }
                    log.Source.IsBoss = true;
                    bossNamesFound.Add(log.Source.LogId.ToString());
                }
                if (bossIds.Contains(log.Target.LogId.ToString()))
                {
                    if (log.Effect.EffectType == EffectType.TargetChanged)
                    {
                        continue;
                    }
                    log.Target.IsBoss = true;
                    bossNamesFound.Add(log.Target.LogId.ToString());
                }
            }

            return bossNamesFound.Distinct().ToList();
        }
        private static void UpdateBossEntities(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return;

            var bossIds = new HashSet<string>(currentEncounter.BossInfos.SelectMany(b => b.TargetIds));

            foreach (var log in logs)
            {
                if (bossIds.Contains(log.Source.LogId.ToString()))
                {
                    log.Source.IsBoss = true;
                }
                if (bossIds.Contains(log.Target.LogId.ToString()))
                {
                    log.Target.IsBoss = true;
                }
            }
        }
    }
}
