using MathNet.Numerics.Statistics;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatMetaDataParse
    {
        private static readonly HashSet<ulong> _interruptAbilityIds = new HashSet<ulong> { 963120646324224, 987747988799488, 875086701658112, 997020823191552, 3433285187272704, 812105301229568, 807750204391424, 2204391964672000, 3029313448312832, 3029339218116608, 875060931854336, 2204499338854400 };
        private static HashSet<ulong> stunAbilityIds = new HashSet<ulong> { 814214130171904, 814802540691456,3908961405239296, 1962284658196480, 808244125630464, 807754499358720, 958439131971584, 807178973741056, 1679250608357376, 1261925816074240 };
        
        private static readonly HashSet<ulong> _cleanseAbilityIds = new HashSet<ulong> { 985007799664640, 3413249164836864, 992541172301824, 981455861710848, 3412806783205376, 952181364621312, 992541172302291, 985007799664916,981455861711254};
        private static HashSet<ulong> abilityIdsThatCanInterrupt => new HashSet<ulong>(_interruptAbilityIds.Concat(stunAbilityIds));
        public static void PopulateMetaData(Combat combatToPopulate)
        {
            var combat = combatToPopulate;

            var cleanseLogs = combat.AllLogs.Values.Where(l =>
l.Effect.EffectType == EffectType.Remove && l.Target.LogId != l.Source.LogId && l.Target.IsCharacter);
            combat.Initiator = combat.AllLogs.OrderBy(kvp=>kvp.Key).FirstOrDefault(l =>
                l.Value.Effect.EffectType == EffectType.TargetChanged && !l.Value.Source.IsCharacter).Value?.Target;
            //Parallel.ForEach(combatToPopulate.AllEntities, entitiy =>
            foreach (var entity in combatToPopulate.AllEntities)
            {
                var logsInScope = combat.GetLogsInvolvingEntity(entity);

                var outgoingLogs = logsInScope.Where(log => log.Source == entity);
                var incomingLogs = logsInScope.Where(log => log.Target == entity);

                var logEntriesForEntity = outgoingLogs as ParsedLogEntry[] ?? outgoingLogs.ToArray();
// 1) outgoing damage
                combat.OutgoingDamageLogs[entity] = new ConcurrentQueue<ParsedLogEntry>(
                    logEntriesForEntity
                        .Where(l =>
                            l.Effect.EffectType == EffectType.Apply &&
                            l.Effect.EffectId   == _7_0LogParsing._damageEffectId &&
                            l.Source.Name       != l.Target.Name
                        )
                );

// 2) outgoing healing
                combat.OutgoingHealingLogs[entity] = new ConcurrentQueue<ParsedLogEntry>(
                    logEntriesForEntity
                        .Where(l =>
                            l.Effect.EffectType == EffectType.Apply &&
                            l.Effect.EffectId   == _7_0LogParsing._healEffectId
                        )
                );

// 3) abilities-activated
                combat.AbilitiesActivated[entity] = new ConcurrentQueue<ParsedLogEntry>(
                    logEntriesForEntity
                        .Where(l =>
                            l.Effect.EffectType == EffectType.Event &&
                            l.Effect.EffectId   == _7_0LogParsing.AbilityActivateId
                        )
                );
                var incomingList = incomingLogs as ParsedLogEntry[] ?? incomingLogs.ToArray();

// 4) incoming damage
                combat.IncomingDamageLogs[entity] = new ConcurrentQueue<ParsedLogEntry>(
                    incomingList
                        .Where(l =>
                            l.Effect.EffectType == EffectType.Apply &&
                            l.Effect.EffectId   == _7_0LogParsing._damageEffectId
                        )
                );

// 5) incoming healing
                combat.IncomingHealingLogs[entity] = new ConcurrentQueue<ParsedLogEntry>(
                    incomingList
                        .Where(l =>
                            l.Effect.EffectType == EffectType.Apply &&
                            l.Effect.EffectId   == _7_0LogParsing._healEffectId
                        )
                );

// 6) big-hit timestamps (still a List<DateTime>)
                var bigDamageTimestamps = GetTimestampOfBigHits(incomingList
                    .Where(l =>
                        l.Effect.EffectType == EffectType.Apply &&
                        l.Effect.EffectId   == _7_0LogParsing._damageEffectId
                    )
                    .ToList()
                );
                combat.BigDamageTimestamps[entity] = bigDamageTimestamps;

// 7) mitigated-damage
                combat.IncomingDamageMitigatedLogs[entity] = new ConcurrentQueue<ParsedLogEntry>(
                    combat.IncomingDamageLogs[entity]
                        .Where(l => l.Value.Modifier != null)
                );

                var totalHealing = combat.OutgoingHealingLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveHealing = combat.OutgoingHealingLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var totalDamage = combat.OutgoingDamageLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveDamage = combat.OutgoingDamageLogs[entity].Sum(l => l.Value.EffectiveDblValue);
                var currentFocusTarget = combat.ParentEncounter?.BossIds;
                if (currentFocusTarget is { Count: > 0 })
                {
                    var bosses = currentFocusTarget.SelectMany(boss => boss.Value.SelectMany(diff => diff.Value)).ToList();

                    totalDamage = combat.OutgoingDamageLogs[entity].Where(d => !bosses.Contains(d.Target.LogId))
                        .Sum(l => l.Value.DblValue);
                    totalEffectiveDamage = combat.OutgoingDamageLogs[entity].Where(d => !bosses.Contains(d.Target.LogId))
                        .Sum(l => l.Value.EffectiveDblValue);
                    var focusDamageLogs = combat.OutgoingDamageLogs[entity].Where(d => bosses.Contains(d.Target.LogId));
                    var damageLogs = focusDamageLogs as ParsedLogEntry[] ?? focusDamageLogs.ToArray();
                    var allFocusDamage = damageLogs.Sum(l => l.Value.DblValue);
                    var allEffectiveFocusDamage = damageLogs.Sum(l => l.Value.EffectiveDblValue);
                    combat.TotalFocusDamage[entity] = allFocusDamage;
                    combat.TotalEffectiveFocusDamage[entity] = allEffectiveFocusDamage;
                }
                else
                {
                    combat.TotalFocusDamage[entity] = 0;
                    combat.TotalEffectiveFocusDamage[entity] = 0;
                }

                var totalAbilitiesDone = logEntriesForEntity.Count(l =>
                    l.Effect.EffectType == EffectType.Event && l.Effect.EffectId == _7_0LogParsing.AbilityActivateId);

                var interruptLogs = logEntriesForEntity.Select((v, i) => new { value = v, index = i }).Where(l =>
                    l.value.Effect.EffectType == EffectType.Event && l.index != 0 &&
                    l.value.Effect.EffectId == _7_0LogParsing.InterruptCombatId &&
                    abilityIdsThatCanInterrupt.Contains(logEntriesForEntity.ElementAt(l.index - 1).AbilityId));

                var mycleanseLogs = logEntriesForEntity.Where(l => _cleanseAbilityIds.Contains(l.AbilityId)).Where(l => cleanseLogs.Any(t => t.LogLineNumber - l.LogLineNumber <= 4 && t.LogLineNumber - l.LogLineNumber > 0));
                var myCleanseSpeeds = mycleanseLogs.Select(cl => GetSpeedFromLog(cl, cleanseLogs));
                var averageCleansespeed = myCleanseSpeeds.Any() ? myCleanseSpeeds.Average() : 0;

                var totalHealingReceived = combat.IncomingHealingLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived =
                    combat.IncomingHealingLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.MitigatedDblValue);

                var sheildingLogs = incomingList.Where(l => l.Value.Modifier is { ValueType: DamageType.shield });

                var enumerable = sheildingLogs as ParsedLogEntry[] ?? sheildingLogs.ToArray();
                var totalSheildingDone = enumerable.Length == 0 ? 0 : enumerable.Sum(l => l.Value.Modifier.DblValue);

                Dictionary<string, double> parriedAttackSums = CalculateEstimatedAvoidedDamage(combat, entity);

                combat.AverageCleanseSpeed[entity] = averageCleansespeed;
                combat.TotalInterrupts[entity] = interruptLogs.Count();
                combat.TotalCleanses[entity] = mycleanseLogs.Count();
                combat.TotalThreat[entity] = 0;
// Step 1: Build PlayerThreatPerEnemy[enemy][player]
                if (!entity.IsCharacter)
                {
                    if (!combat.PlayerThreatPerEnemy.ContainsKey(entity))
                        combat.PlayerThreatPerEnemy[entity] = new Dictionary<Entity, double>();

                    foreach (var group in incomingLogs.GroupBy(l => l.Source))
                    {
                        var player = group.Key;
                        double threat = 0;
                        foreach (var log in group)
                        {
                            threat = Math.Max(0, threat + log.Threat);
                        }
                        combat.PlayerThreatPerEnemy[entity][player] = threat;
                    }
                }

// Step 2: Later — after all enemies processed — rebuild TotalThreat[player]
                
                foreach (var enemyKvp in combat.PlayerThreatPerEnemy)
                {
                    foreach (var playerKvp in enemyKvp.Value)
                    {
                        if (playerKvp.Key != entity)
                            continue;
                        var player = playerKvp.Key;
                        var threat = playerKvp.Value;

                        if (!combat.TotalThreat.ContainsKey(player))
                            combat.TotalThreat[player] = 0;

                        combat.TotalThreat[player] += threat;
                    }
                }
                
                combat.MaxDamage[entity] = combat.OutgoingDamageLogs[entity].Count == 0
                    ? 0
                    : combat.OutgoingDamageLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxEffectiveDamage[entity] = combat.OutgoingDamageLogs[entity].Count == 0
                    ? 0
                    : combat.OutgoingDamageLogs[entity].Max(l => l.Value.EffectiveDblValue);
                combat.MaxHeal[entity] = combat.OutgoingHealingLogs[entity].Count == 0
                    ? 0
                    : combat.OutgoingHealingLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxEffectiveHeal[entity] = combat.OutgoingHealingLogs[entity].Count == 0
                    ? 0
                    : combat.OutgoingHealingLogs[entity].Max(l => l.Value.EffectiveDblValue);
                combat.TotalFluffDamage[entity] = totalDamage;
                combat.TotalEffectiveFluffDamage[entity] = totalEffectiveDamage;
                combat.TotalTankSheilding[entity] = totalSheildingDone;
                combat.TotalEstimatedAvoidedDamage[entity] = parriedAttackSums.Sum(kvp => kvp.Value);
                combat.TotalSheildAndAbsorb[entity] = combat.IncomingDamageMitigatedLogs[entity].Count == 0
                    ? 0
                    : combat.IncomingDamageMitigatedLogs[entity].Sum(l => l.Value.Modifier.EffectiveDblValue);
                combat.TotalAbilites[entity] = totalAbilitiesDone;
                combat.TotalHealing[entity] = totalHealing;
                combat.TotalEffectiveHealing[entity] = totalEffectiveHealing;
                combat.TotalDamageTaken[entity] = totalDamageTaken + combat.TotalEstimatedAvoidedDamage[entity];
                combat.TotalEffectiveDamageTaken[entity] = totalEffectiveDamageTaken;
                combat.TotalHealingReceived[entity] = totalHealingReceived;
                combat.TotalEffectiveHealingReceived[entity] = totalEffectiveHealingReceived;
                combat.MaxIncomingDamage[entity] = combat.IncomingDamageLogs[entity].Count == 0
                    ? 0
                    : combat.IncomingDamageLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxEffectiveIncomingDamage[entity] = combat.IncomingDamageLogs[entity].Count == 0
                    ? 0
                    : combat.IncomingDamageLogs[entity].Max(l => l.Value.MitigatedDblValue);
                combat.MaxIncomingHeal[entity] = combat.IncomingHealingLogs[entity].Count == 0
                    ? 0
                    : combat.IncomingHealingLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxIncomingEffectiveHeal[entity] = combat.IncomingHealingLogs[entity].Count == 0
                    ? 0
                    : combat.IncomingHealingLogs[entity].Max(l => l.Value.EffectiveDblValue);
            }

            if ((combat.DurationSeconds % 50) == 0 || !combat.HasBurstValues() || combat.AllBurstDamages.Keys.Count != combat.CharacterParticipants.Count)
                combat.SetBurstValues();

            var healers = combat.CharacterParticipants.Where(p => CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(p, combat.EndTime).Role == Role.Healer);
            var tanks = combat.CharacterParticipants.Where(p => CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(p, combat.EndTime).Role == Role.Tank);

            foreach (var healer in healers)
            {
                var abilityActivateTimesOnTargets = GetTimestampsOfAbilitiesOnPlayers(combat.AbilitiesActivated[healer]);
                var reactionTimesToBigHigs = CalculateReactionToBigHits(combat.BigDamageTimestamps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), abilityActivateTimesOnTargets);
                combat.AllDamageRecoveryTimes[healer] = reactionTimesToBigHigs;
                var reactionTimesToBigHigsOnTanks = CalculateReactionToBigHits(combat.BigDamageTimestamps.Where(kvp => tanks.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value), abilityActivateTimesOnTargets);
                combat.TankDamageRecoveryTimes[healer] = reactionTimesToBigHigsOnTanks;
            }
        }

        public static void ApplyIncrementalMetaData(Combat combat, IEnumerable<ParsedLogEntry> newLogs)
        {
// lazy‐init every dictionary/list for brand‐new entities
            void EnsureEntity(Entity e)
            {
                if(!combat.AllEntities.Contains(e))
                    combat.AllEntities.Add(e);
                
                // per‐entity buckets of parsed logs
                if (!combat.TotalAbilites.ContainsKey(e))
                {
                    combat.OutgoingDamageLogs[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.IncomingDamageLogs[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.IncomingDamageMitigatedLogs[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.OutgoingHealingLogs[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.IncomingHealingLogs[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.ShieldingProvidedLogs[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.AbilitiesActivated[e] = new ConcurrentQueue<ParsedLogEntry>();
                    combat.PlayerThreatPerEnemy[e] = new Dictionary<Entity, double>();

                    // numeric counters & running sums
                    combat.TotalFluffDamage[e] = 0;
                    combat.TotalEffectiveFluffDamage[e] = 0;
                    combat.TotalFocusDamage[e] = 0;
                    combat.TotalEffectiveFocusDamage[e] = 0;
                    combat.TotalCompanionDamage[e] = 0;
                    combat.TotalDamage[e] = 0;
                    combat.TotalEffectiveDamage[e] = 0;
                    combat.MaxDamage[e] = 0;
                    combat.MaxEffectiveDamage[e] = 0;

                    combat.TotalDamageTaken[e] = 0;
                    combat.TotalEffectiveDamageTaken[e] = 0;
                    combat.MaxIncomingDamage[e] = 0;
                    combat.MaxEffectiveIncomingDamage[e] = 0;
                    combat.TotalSheildAndAbsorb[e] = 0;
                    combat.TotalProvidedSheilding[e] = 0;
                    combat.TotalEstimatedAvoidedDamage[e] = 0;

                    combat.TotalHealing[e] = 0;
                    combat.TotalEffectiveHealing[e] = 0;
                    combat.MaxHeal[e] = 0;
                    combat.MaxEffectiveHeal[e] = 0;
                    combat.TotalHealingReceived[e] = 0;
                    combat.TotalEffectiveHealingReceived[e] = 0;
                    combat.MaxIncomingHeal[e] = 0;
                    combat.MaxIncomingEffectiveHeal[e] = 0;

                    combat.TotalCompanionHealing[e] = 0;
                    combat.TotalEffectiveCompanionHealing[e] = 0;
                    
                    combat.TotalTankSheilding[e] = 0;
                    combat.TotalAbilites[e] = 0;
                    combat.TotalInterrupts[e] = 0;
                    combat.TotalThreat[e] = 0;
                    combat.TotalCleanses[e] = 0;
                    combat.AverageCleanseSpeed[e] = 0;
                    combat.AverageDamageSavedDuringCooldown[e] = 0;
                    combat.TimeSpentBelowFullHealth[e] = 0;

                    combat.BigDamageTimestamps[e] = new List<DateTime>();

                    // phase‐/burst‐related (if you reference them before first SetBurstValues)
                    if (!combat.AllBurstDamages.ContainsKey(e)) combat.AllBurstDamages[e] = new List<Point>();
                    if (!combat.AllBurstDamageTakens.ContainsKey(e)) combat.AllBurstDamageTakens[e] = new List<Point>();
                    if (!combat.AllBurstHealings.ContainsKey(e)) combat.AllBurstHealings[e] = new List<Point>();
                    if (!combat.AllBurstHealingReceived.ContainsKey(e))
                        combat.AllBurstHealingReceived[e] = new List<Point>();

                    // reaction‐time buckets (recomputed every update)
                    if (!combat.AllDamageRecoveryTimes.ContainsKey(e))
                        combat.AllDamageRecoveryTimes[e] = new Dictionary<Entity, List<double>>();
                    if (!combat.TankDamageRecoveryTimes.ContainsKey(e))
                        combat.TankDamageRecoveryTimes[e] = new Dictionary<Entity, List<double>>();
                }
            }

            // 1) Precompute the set of current boss IDs for focus vs fluff damage
            var bossIds = new HashSet<long>();
            if (combat.ParentEncounter?.BossIds != null)
            {
                foreach (var diffMap in combat.ParentEncounter.BossIds.Values)
                foreach (var list in diffMap.Values)
                foreach (var id in list)
                    bossIds.Add(id);
            }

            // 2) Fold in each new log entry
            foreach (var log in newLogs)
            {
                var src = log.Source;
                var tgt = log.Target;
                var et = log.Effect.EffectType;
                var eid = log.Effect.EffectId;
                var val = log.Value;

                // ensure we have a slot for both source and target
                EnsureEntity(src);
                EnsureEntity(tgt);
                
                // any log with a positive threat value, regardless of effect type:
                var thr = log.Threat;
                if (thr > 0)
                {
                    if (!log.Target.IsCharacter)
                    {
                        // enemy = log.Target, player = log.Source
                        var threatDict = combat.PlayerThreatPerEnemy[log.Target];
                        // increment the per‐enemy, per‐player bucket
                        threatDict[log.Source] = threatDict.TryGetValue(log.Source, out var soFar)
                            ? soFar + thr
                            : thr;

                        // and increment the running total for that player
                        combat.TotalThreat[log.Source] += thr;
                    }

                }
                
                // ─── OUTGOING DAMAGE ─────────────────────────────────────────────────────
                if (et == EffectType.Apply
                    && eid == _7_0LogParsing._damageEffectId
                    && src != tgt)
                {
                    combat.OutgoingDamageLogs[src].Enqueue(log);

                    // running sums & maxima
                    double dmg = val.DblValue;
                    double edmg = val.EffectiveDblValue;
                    combat.TotalFluffDamage[src] += bossIds.Contains(tgt.LogId) ? 0 : dmg;
                    combat.TotalEffectiveFluffDamage[src] += bossIds.Contains(tgt.LogId) ? 0 : edmg;
                    combat.TotalFocusDamage[src] += bossIds.Contains(tgt.LogId) ? dmg : 0;
                    combat.TotalEffectiveFocusDamage[src] += bossIds.Contains(tgt.LogId) ? edmg : 0;

                    combat.TotalDamage[src] += dmg;
                    combat.TotalEffectiveDamage[src] += edmg;
                    combat.MaxDamage[src] = Math.Max(combat.MaxDamage[src], dmg);
                    combat.MaxEffectiveDamage[src] = Math.Max(combat.MaxEffectiveDamage[src], edmg);
                }

                // ─── INCOMING DAMAGE ────────────────────────────────────────────────────
                if (et == EffectType.Apply
                    && eid == _7_0LogParsing._damageEffectId)
                {
                    combat.IncomingDamageLogs[tgt].Enqueue(log);

                    double taken = val.DblValue;
                    double mitigated = val.MitigatedDblValue;
                    combat.TotalDamageTaken[tgt] += taken;
                    combat.TotalEffectiveDamageTaken[tgt] += val.EffectiveDblValue;
                    combat.MaxIncomingDamage[tgt] = Math.Max(combat.MaxIncomingDamage[tgt], taken);
                    combat.MaxEffectiveIncomingDamage[tgt] =
                        Math.Max(combat.MaxEffectiveIncomingDamage[tgt], mitigated);

                    // shields/absorbs
                    if (val.Modifier != null)
                    {
                        combat.IncomingDamageMitigatedLogs[tgt].Enqueue(log);
                        combat.TotalSheildAndAbsorb[tgt] += val.Modifier.EffectiveDblValue;
                    }
// inside your “incoming damage” block, where you spot a shield modifier:
                    if (val.Modifier != null && val.Modifier.ValueType == DamageType.shield)
                    {
                        combat.ShieldingProvidedLogs[tgt].Enqueue(log);
                        combat.TotalTankSheilding[tgt] += val.Modifier.DblValue;
                    }
                    // big hits (5% thresholds)
                    var bigs = GetTimestampOfBigHits([log]);
                    if (bigs.Count > 0)
                        combat.BigDamageTimestamps[tgt].AddRange(bigs);
                }

                // ─── OUTGOING HEALING ────────────────────────────────────────────────────
                if (et == EffectType.Apply
                    && eid == _7_0LogParsing._healEffectId)
                {
                    combat.OutgoingHealingLogs[src].Enqueue(log);

                    double heal = val.DblValue;
                    double eheal = val.EffectiveDblValue;
                    combat.TotalHealing[src] += heal;
                    combat.TotalEffectiveHealing[src] += eheal;
                    combat.MaxHeal[src] = Math.Max(combat.MaxHeal[src], heal);
                    combat.MaxEffectiveHeal[src] = Math.Max(combat.MaxEffectiveHeal[src], eheal);
                }

                // ─── INCOMING HEALING ───────────────────────────────────────────────────
                if (et == EffectType.Apply
                    && eid == _7_0LogParsing._healEffectId)
                {
                    combat.IncomingHealingLogs[tgt].Enqueue(log);

                    double rec = val.DblValue;
                    double erec = val.EffectiveDblValue;
                    combat.TotalHealingReceived[tgt] += rec;
                    combat.TotalEffectiveHealingReceived[tgt] += erec;
                    combat.MaxIncomingHeal[tgt] = Math.Max(combat.MaxIncomingHeal[tgt], rec);
                    combat.MaxIncomingEffectiveHeal[tgt] = Math.Max(combat.MaxIncomingEffectiveHeal[tgt], erec);
                }

                // ─── ABILITY ACTIVATION ─────────────────────────────────────────────────
                if (et == EffectType.Event
                    && eid == _7_0LogParsing.AbilityActivateId)
                {
                    combat.AbilitiesActivated[src].Enqueue(log);
                    combat.TotalAbilites[src]++;
                }

                // ─── INTERRUPTS & THREAT ────────────────────────────────────────────────
                if (et == EffectType.Event
                    && eid == _7_0LogParsing.InterruptCombatId)
                {
                    combat.TotalInterrupts[src]++;
                }

                // ─── CLEANSES ───────────────────────────────────────────────────────────
                if (et == EffectType.Event
                    && _cleanseAbilityIds.Contains(log.AbilityId))
                {
                    combat.TotalCleanses[src]++;

                    // incremental average speed
                    double speed = GetSpeedFromLog(log,
                        combat.AllLogs.Values.Where(l =>
                            l.Effect.EffectType == EffectType.Remove &&
                            _cleanseAbilityIds.Contains(l.AbilityId) &&
                            l.Target.LogId == src.LogId));

                    double count = combat.TotalCleanses[src];
                    combat.AverageCleanseSpeed[src] =
                        ((combat.AverageCleanseSpeed[src] * (count - 1)) + speed)
                        / count;
                }
            }

            // 3) Burst values (every ~50s or if missing)
            if ((combat.DurationSeconds % 50) == 0
                || !combat.HasBurstValues()
                || combat.AllBurstDamages.Keys.Count != combat.CharacterParticipants.Count)
            {
                combat.SetBurstValues();
            }

            // 4) Recompute healers’ reaction times (uses the full
            //    BigDamageTimestamps and AbilitiesActivated lists):
            var healers = combat.CharacterParticipants
                .Where(p => CombatLogStateBuilder.CurrentState
                    .GetCharacterClassAtTime(p, combat.EndTime)
                    .Role == Role.Healer);

            var tanks = combat.CharacterParticipants
                .Where(p => CombatLogStateBuilder.CurrentState
                    .GetCharacterClassAtTime(p, combat.EndTime)
                    .Role == Role.Tank)
                .ToList();

            foreach (var healer in healers)
            {
                var abilityTimes = GetTimestampsOfAbilitiesOnPlayers(
                    combat.AbilitiesActivated[healer]);

                var allRx = CalculateReactionToBigHits(
                    combat.BigDamageTimestamps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    abilityTimes);
                combat.AllDamageRecoveryTimes[healer] = allRx;

                var tankRx = CalculateReactionToBigHits(
                    combat.BigDamageTimestamps
                        .Where(kvp => tanks.Contains(kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    abilityTimes);
                combat.TankDamageRecoveryTimes[healer] = tankRx;
            }
            
            // ─── 5) **Recompute “avoided damage”** ─────────────────────────────────────
            // for each target that got new damage logs, recalc the full avoidance sum,
            // take the delta vs. the old value, and apply it here.
            var damagedEntities = newLogs
                .Where(l => l.Effect.EffectType == EffectType.Apply
                            && l.Effect.EffectId   == _7_0LogParsing._damageEffectId)
                .Select(l => l.Target)
                .Distinct();

            foreach (var entity in damagedEntities)
            {
                // new full sum of all avoided (parry/deflect/dodge/resist)
                var parriedSums   = CalculateEstimatedAvoidedDamage(combat, entity);
                var newEstimated  = parriedSums.Sum(kvp => kvp.Value);
                var prevEstimated = combat.TotalEstimatedAvoidedDamage[entity];
                var diff          = newEstimated - prevEstimated;

                if (diff != 0)
                {
                    combat.TotalEstimatedAvoidedDamage[entity] = newEstimated;
                    // keep “damage taken” in sync with initial PopulateMetaData’s +avoided logic
                    combat.TotalDamageTaken[entity]           += diff;
                }
            }
        }

        private static double GetSpeedFromLog(ParsedLogEntry cl, IEnumerable<ParsedLogEntry> effectRemoveLogs)
        {
            var removedEffectLog = effectRemoveLogs.OrderBy(l=>l.LogLineNumber).FirstOrDefault(l => l.LogLineNumber > cl.LogLineNumber);
            if (removedEffectLog == null)
                return 0;
            var cleanseTime = removedEffectLog.TimeStamp;
            var effectInQuestion = removedEffectLog.Effect.EffectId;
            var hasModifier = CombatLogStateBuilder.CurrentState.Modifiers.TryGetValue(effectInQuestion, out var modifiersForCleansedEffect);
            if (!hasModifier)
                return 0;
            var orderedModifiers = modifiersForCleansedEffect.Values.ToList().OrderBy(l => l.StartTime);
            var removedMod = orderedModifiers.LastOrDefault(l => l.StopTime == DateTime.MinValue || l.StopTime == cleanseTime);
            if (removedMod != null)
            {
                return (cl.TimeStamp - removedMod.StartTime).TotalSeconds;
            }
            return 0;
        }

        private static Dictionary<Entity, List<double>> CalculateReactionToBigHits(Dictionary<Entity, List<DateTime>> bigHitTimestamps, Dictionary<Entity, List<DateTime>> reactionTimeStamps)
        {
            var delays = new Dictionary<Entity, List<double>>();
            foreach (var target in bigHitTimestamps.Keys.Where(e => e.IsCharacter))
            {
                if (!delays.ContainsKey(target))
                    delays[target] = new List<double>();
                if (!reactionTimeStamps.TryGetValue(target,out var reactionsForTarget))
                    continue;
                foreach (var hit in bigHitTimestamps[target])
                {
                    var reactionAfterHit = GetNextBiggerTimestamp(hit, reactionsForTarget);
                    if (!reactionAfterHit.HasValue)
                        break;
                    var differenceSec = (reactionAfterHit.Value - hit).TotalSeconds;
                    if (differenceSec > 10)
                        continue;
                    delays[target].Add(differenceSec);
                }
            }
            return delays;
        }
        private static DateTime? GetNextBiggerTimestamp(DateTime comparison, List<DateTime> values)
        {
            foreach (var t in values)
            {
                if (t > comparison)
                    return t;
            }

            return null;
        }
        private static List<DateTime> GetTimestampOfBigHits(List<ParsedLogEntry> incomingDamage)
        {
            var timestamps = new List<DateTime>();
            if (incomingDamage.Count == 0)
                return timestamps;

            var threshold = incomingDamage.First().TargetInfo.MaxHP * 0.05;
            List<(DateTime, double)> oneSecondOfDamage = new List<(DateTime, double)>();
            foreach (var damage in incomingDamage)
            {
                if (damage.Value.EffectiveDblValue >= threshold)
                {
                    timestamps.Add(damage.TimeStamp);
                    oneSecondOfDamage.Clear();
                }
                else
                {
                    oneSecondOfDamage.Add((damage.TimeStamp, damage.Value.EffectiveDblValue));
                    oneSecondOfDamage.RemoveAll(v => (damage.TimeStamp - v.Item1) > TimeSpan.FromSeconds(1));
                    var totalDamageOverLastSecond = oneSecondOfDamage.Select(v => v.Item2).Sum();
                    if (totalDamageOverLastSecond >= threshold)
                    {
                        timestamps.Add(damage.TimeStamp);
                        oneSecondOfDamage.Clear();
                    }
                }
            }
            return timestamps;
        }
        private static Dictionary<Entity, List<DateTime>> GetTimestampsOfAbilitiesOnPlayers(ConcurrentQueue<ParsedLogEntry> abilityActivateLogs)
        {
            var returnDict = new Dictionary<Entity, List<DateTime>>();
            foreach (var abilityActivation in abilityActivateLogs.Where(l => l.Target.IsCharacter))
            {
                var target = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(abilityActivation.Source, abilityActivation.TimeStamp).Entity;
                if (target == null)
                    continue;
                if (!returnDict.ContainsKey(target))
                    returnDict[target] = new List<DateTime>();
                returnDict[target].Add(abilityActivation.TimeStamp);
            }
            return returnDict;
        }
        private static Dictionary<string, double> CalculateEstimatedAvoidedDamage(Combat combatToPopulate, Entity participant)
        {
            var totallyMitigatedAttacks = combatToPopulate.IncomingDamageLogs[participant].Where(l =>
                l.Value.ValueType == DamageType.parry ||
                l.Value.ValueType == DamageType.deflect ||
                l.Value.ValueType == DamageType.dodge ||
                l.Value.ValueType == DamageType.resist
            );
            Dictionary<string, double> parriedAttackSums = new Dictionary<string, double>();
            var damageDone = combatToPopulate.GetIncomingDamageByAbility(participant);
            var parsedLogEntries = totallyMitigatedAttacks as ParsedLogEntry[] ?? totallyMitigatedAttacks.ToArray();
            foreach (var mitigatedAttack in parsedLogEntries.Select(l => l.Ability).Distinct())
            {
                var numberOfParries = parsedLogEntries.Count(l => l.Ability == mitigatedAttack);
                var damageFromUnparriedAttacks = damageDone[mitigatedAttack].Select(v => v.Value.EffectiveDblValue).Where(v => v > 0);
                var fromUnparriedAttacks = damageFromUnparriedAttacks as double[] ?? damageFromUnparriedAttacks.ToArray();
                if (fromUnparriedAttacks.Length == 0)
                    continue;
                var averageDamageFromUnparriedAttack = fromUnparriedAttacks.Mean() * numberOfParries;
                parriedAttackSums[mitigatedAttack] = averageDamageFromUnparriedAttack;
            }

            return parriedAttackSums;
        }

        public static Dictionary<string, double> GetAverage(Dictionary<string, List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        {
            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                if (!checkEffective)
                    returnDict[kvp.Key] = kvp.Value.Average(v => v.Value.DblValue);
                else
                    returnDict[kvp.Key] = kvp.Value.Average(v => v.Value.EffectiveDblValue);
            }
            return returnDict;
        }
        public static Dictionary<string, double> GetMax(Dictionary<string, List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        {
            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                if (!checkEffective)
                    returnDict[kvp.Key] = kvp.Value.Max(v => v.Value.DblValue);
                else
                    returnDict[kvp.Key] = kvp.Value.Max(v => v.Value.EffectiveDblValue);
            }
            return returnDict;
        }
        public static Dictionary<string, double> GetSum(Dictionary<string, List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        {
            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                if (!checkEffective)
                    returnDict[kvp.Key] = kvp.Value.Sum(v => v.Value.DblValue);
                else
                    returnDict[kvp.Key] = kvp.Value.Sum(v => v.Value.EffectiveDblValue);
            }
            return returnDict;
        }
        public static Dictionary<string, double> Getcount(Dictionary<string, List<ParsedLogEntry>> combatMetaData)
        {
            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                returnDict[kvp.Key] = kvp.Value.Count();
            }
            return returnDict;
        }
        public static Dictionary<string, double> GetCritPercent(Dictionary<string, List<ParsedLogEntry>> combatMetaData)
        {
            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                returnDict[kvp.Key] = kvp.Value.Count(v => v.Value.WasCrit) / (double)kvp.Value.Count();
            }
            return returnDict;
        }
        public static Dictionary<string, double> GetEffectiveHealsPercent(Dictionary<string, List<ParsedLogEntry>> combatMetaData)
        {
            var sumEffective = GetSum(combatMetaData, true);
            var sumTotal = GetSum(combatMetaData);

            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                returnDict[kvp.Key] = sumEffective[kvp.Key] / sumTotal[kvp.Key];
            }
            return returnDict;
        }
    }
}
