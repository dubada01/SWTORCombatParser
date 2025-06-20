using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatIdentifier
    {
        public static event Action<Combat> CombatFinished = delegate { };
        public static Combat CurrentCombat { get; set; }

        /// <summary>
        /// Completely resets the internal Combat state.
        /// Call this at the start of a new fight.
        /// </summary>
        public static void ResetCombat()
        {

            CurrentCombat = new Combat();

        }

        /// <summary>
        /// Merges logs into the current Combat. If `reset` is true, calls ResetCombat() first.
        /// Use this both for initial bootstrapping (reset=true) or incremental updates (reset=false).
        /// </summary>
        public static Combat GenerateCombatFromLogs(
            IEnumerable<ParsedLogEntry> logs,
            Combat? combatShell = null,
            bool isRealtime = false,
            bool combatEndUpdate = false,
            bool isOverallCombat = false,int overallCombatDuration = 0)
        {
            var combatToUpdate = combatShell ?? CurrentCombat;

            var isFirstUpdate = false;
            var parsedLogEntries = logs as ParsedLogEntry[] ?? logs.ToArray();
            if ((!parsedLogEntries.Any() && !combatEndUpdate))
                return combatToUpdate;
            if (!parsedLogEntries.Any() && combatEndUpdate)
                CombatFinished.InvokeSafely(combatToUpdate);


            var orderedLogs = parsedLogEntries.OrderBy(l => l.TimeStamp).ToList();

            // Determine encounter time from the very first valid timestamp
            var firstTime = combatToUpdate.StartTime == default
                ? orderedLogs.FirstOrDefault(l => l.TimeStamp != DateTime.MinValue)?.TimeStamp
                : combatToUpdate.StartTime;
            if (!firstTime.HasValue)
                return combatToUpdate;
            var encounter = GetEncounterInfo(firstTime.Value);

            // If this was a reset bootstrap, set StartTime

            if (combatToUpdate.StartTime == DateTime.MinValue)
            {
                combatToUpdate.StartTime = firstTime.Value;
                isFirstUpdate = true;
            }

            foreach (var log in orderedLogs)
            {
                combatToUpdate.AllLogs[log.LogLineNumber] = log;
                MergeEntityLog(log.Source, log, combatToUpdate);
                if (log.Source != log.Target)
                    MergeEntityLog(log.Target, log, combatToUpdate);

                if (log.Source.IsCharacter || log.Source.IsCompanion)
                    AddParticipant(log.Source, log.TimeStamp, combatToUpdate);
                if (log.Target.IsCharacter || log.Target.IsCompanion)
                    AddParticipant(log.Target, log.TimeStamp, combatToUpdate);
                AddTarget(log, combatToUpdate);

                // Always update EndTime to the latest
                if (!isOverallCombat)
                    combatToUpdate.EndTime = log.TimeStamp;
            }
            
            if (isOverallCombat)
                combatToUpdate.EndTime = combatToUpdate.StartTime.AddSeconds(overallCombatDuration);

            foreach (var entity in combatToUpdate.AllEntities.Select(e => e.LogId))
            {
                if (!combatToUpdate.LogsInvolvingEntity.ContainsKey(entity))
                    combatToUpdate.LogsInvolvingEntity = new Dictionary<long, ConcurrentQueue<ParsedLogEntry>>();
            }

            // Always refresh boss/encounter info
            if (encounter != null && encounter.BossInfos != null)
            {
                combatToUpdate.ParentEncounter = encounter;
                combatToUpdate.EncounterBossDifficultyParts = GetCurrentBossInfo(combatToUpdate.AllLogs.Values, encounter);
                combatToUpdate.BossInfo = GetCurrentBossInfoObject(combatToUpdate.AllLogs.Values, encounter);
                UpdateBossEntities(combatToUpdate.AllLogs.Values, encounter);
            }

            PostMetadata(isRealtime, combatEndUpdate, orderedLogs, isFirstUpdate, combatToUpdate);
            return combatToUpdate;

        }

        /// <summary>
        /// One-call full build + snapshot: returns only the cloned Combat.
        /// Avoids local double-assignment and extra GC pressure.
        /// </summary>
        public static Combat GenerateCombatSnapshotFromLogs(
            IEnumerable<ParsedLogEntry> logs,
            bool isRealtime = false,
            bool combatEndUpdate = false)
        {
            var combatShell = new Combat();
            // build into CurrentCombat without capturing its return
            GenerateCombatFromLogs(logs, combatShell: combatShell, isRealtime, combatEndUpdate);
            // return only the snapshot instance
            return combatShell;
        }
        /// <summary>
        /// One-call full build + snapshot: returns only the cloned Combat.
        /// Avoids local double-assignment and extra GC pressure.
        /// </summary>
        public static Combat GenerateOverallCombat(
            IEnumerable<ParsedLogEntry> logs,
            bool isRealtime = false,
            bool combatEndUpdate = false,
            int overallCombatDuration = 0)
        {
            var combatShell = new Combat();
            // build into CurrentCombat without capturing its return
            GenerateCombatFromLogs(logs, combatShell: combatShell, isRealtime,  combatEndUpdate, true,overallCombatDuration);
            // return only the snapshot instance
            return combatShell;
        }
 /// <summary>
        /// Shared post-merge logic: metadata, shields, cooldowns, events.
        /// </summary>
        private static void PostMetadata(bool isRealtime, bool combatEndUpdate, List<ParsedLogEntry> newLogs, bool isFirstUpdate, Combat combatToUpdate)
        {
            if(isFirstUpdate)
                CombatMetaDataParse.PopulateMetaData(combatToUpdate);
            else
                CombatMetaDataParse.ApplyIncrementalMetaData(combatToUpdate, newLogs);

            var absorbLogs = combatToUpdate.IncomingDamageMitigatedLogs
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value
                        .Where(e => e.Value.Modifier.ValueType == DamageType.absorbed)
                        .OrderBy(e => e.TimeStamp)
                        .ToList()
                );
            ShieldingProcessor.AddShieldLogsByTarget(absorbLogs, combatToUpdate);
            TankCooldownProcessor.AddDamageSavedDuringCooldown(combatToUpdate);

            if (combatToUpdate.IsCombatWithBoss)
            {
                var parts = combatToUpdate.EncounterBossDifficultyParts;
                EncounterTimerTrigger.FireBossCombatDetected(
                    combatToUpdate.ParentEncounter.Name,
                    parts.Item1,
                    combatToUpdate.ParentEncounter.Difficutly, isRealtime);
            }

            if (combatEndUpdate)
                CombatFinished.InvokeSafely(combatToUpdate);
        }

        private static void MergeEntityLog(Entity e, ParsedLogEntry log, Combat combatToUpdate)
        {            
            if (!combatToUpdate.LogsInvolvingEntity.TryGetValue(e.LogId, out var list))
            {
                list = new ConcurrentQueue<ParsedLogEntry>();
                combatToUpdate.LogsInvolvingEntity[e.LogId] = list;
            }
            list.Enqueue(log);
        }

        private static void AddParticipant(Entity e, DateTime timestamp, Combat combatToUpdate)
        {
            if (combatToUpdate.CharacterParticipants.All(p => p.LogId != e.LogId))
            {
                combatToUpdate.CharacterParticipants.Add(e);
                combatToUpdate.CharacterClases[e] = CombatLogStateBuilder.CurrentState
                    .GetCharacterClassAtTime(e, timestamp);
            }
        }

        private static void AddTarget(ParsedLogEntry log, Combat combatToUpdate)
        {
            if (log.Effect.EffectType != EffectType.TargetChanged && log.Effect.EffectId == _7_0LogParsing._damageEffectId)
            {
                if (log.Target != null && (!log.Target.IsCharacter || CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(log.Target, combatToUpdate.StartTime)) && !log.Target.IsCompanion && log.Target.Name != null)
                {
                    if (combatToUpdate.Targets.All(t => t.Id != log.Target.Id))
                        combatToUpdate.Targets.Add(log.Target);
                }

                if (log.Source != null && (!log.Source.IsCharacter || CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(log.Source, combatToUpdate.StartTime)) && !log.Source.IsCompanion && log.Source.Name != null)
                {
                    if (combatToUpdate.Targets.All(t => t.Id != log.Source.Id))
                        combatToUpdate.Targets.Add(log.Source);
                }
            }
        }
        
        private static EncounterInfo GetEncounterInfo(DateTime combatStartTime)
        {
            return CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(combatStartTime);
        }
        public static (string, string, string) GetCurrentBossInfo(IEnumerable<ParsedLogEntry> logs, EncounterInfo currentEncounter)
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
                        return (owb.BossName, "1", "Open World");
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
        public static BossInfo GetCurrentBossInfoObject(IEnumerable<ParsedLogEntry> logs, EncounterInfo currentEncounter)
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
        private static List<long> GetCurrentBossNames(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null)
                return new List<long>();

            var bossIds = new HashSet<long>(currentEncounter.BossInfos.SelectMany(b => b.TargetIds));

            var bossNamesFound = new List<long>();
            foreach (var log in logs)
            {
                if (bossIds.Contains(log.Source.LogId))
                {
                    if (log.Effect.EffectType == EffectType.Remove)
                    {
                        continue;
                    }
                    log.Source.IsBoss = true;
                    bossNamesFound.Add(log.Source.LogId);
                }
                if (bossIds.Contains(log.Target.LogId))
                {
                    if (log.Effect.EffectType == EffectType.TargetChanged)
                    {
                        continue;
                    }
                    log.Target.IsBoss = true;
                    bossNamesFound.Add(log.Target.LogId);
                }
            }

            return bossNamesFound.Distinct().ToList();
        }
        private static void UpdateBossEntities(IEnumerable<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return;

            var bossIds = new HashSet<long>(currentEncounter.BossInfos.SelectMany(b => b.TargetIds));

            foreach (var log in logs)
            {
                if (bossIds.Contains(log.Source.LogId))
                {
                    log.Source.IsBoss = true;
                }
                if (bossIds.Contains(log.Target.LogId))
                {
                    log.Target.IsBoss = true;
                }
            }
        }
    }
}
