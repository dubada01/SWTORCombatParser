using MathNet.Numerics.Statistics;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatMetaDataParse
    {
        private static readonly HashSet<string> _interruptAbilityIds = new HashSet<string> { "963120646324224", "987747988799488", "875086701658112", "997020823191552", "3433285187272704", "812105301229568", "807750204391424", "2204391964672000", "3029313448312832", "3029339218116608", "875060931854336", "2204499338854400" };
        private static HashSet<string> stunAbilityIds = new HashSet<string> { "814214130171904", "814802540691456", "3908961405239296", "1962284658196480", "808244125630464", "807754499358720", "958439131971584", "807178973741056", "1679250608357376", "1261925816074240" };
        
        private static readonly HashSet<string> _cleanseAbilityIds = new HashSet<string> { "985007799664640", "3413249164836864", "992541172301824", "981455861710848", "3412806783205376", "952181364621312", "992541172302291", "985007799664916" };
        private static HashSet<string> abilityIdsThatCanInterrupt => new HashSet<string>(_interruptAbilityIds.Concat(stunAbilityIds));
        public static void PopulateMetaData(Combat combatToPopulate)
        {
            var combat = combatToPopulate;

            var cleanseLogs = combat.AllLogs.Where(l =>
l.Effect.EffectType == EffectType.Remove && l.Target.LogId != l.Source.LogId && l.Target.IsCharacter);

            //Parallel.ForEach(combatToPopulate.AllEntities, entitiy =>
            foreach (var entity in combatToPopulate.AllEntities)
            {
                var logsInScope = combat.GetLogsInvolvingEntity(entity);

                var outgoingLogs = logsInScope.Where(log => log.Source == entity);
                var incomingLogs = logsInScope.Where(log => log.Target == entity);

                var parsedLogEntries = outgoingLogs as ParsedLogEntry[] ?? outgoingLogs.ToArray();
                combat.OutgoingDamageLogs[entity] = parsedLogEntries
                    .Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectId == _7_0LogParsing._damageEffectId).ToList();
                combat.OutgoingHealingLogs[entity] = parsedLogEntries
                    .Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectId == _7_0LogParsing._healEffectId).ToList();
                combat.AbilitiesActivated[entity] = parsedLogEntries.Where(l =>
                    l.Effect.EffectType == EffectType.Event && l.Effect.EffectId == _7_0LogParsing.AbilityActivateId).ToList();
                var logEntries = incomingLogs as ParsedLogEntry[] ?? incomingLogs.ToArray();
                combat.IncomingDamageLogs[entity] = logEntries
                    .Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectId == _7_0LogParsing._damageEffectId).ToList();
                combat.IncomingHealingLogs[entity] = logEntries
                    .Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectId == _7_0LogParsing._healEffectId).ToList();

                var bigDamageTimestamps = GetTimestampOfBigHits(combat.IncomingDamageLogs[entity]);
                combat.BigDamageTimestamps[entity] = bigDamageTimestamps;

                combat.IncomingDamageMitigatedLogs[entity] =
                    combat.IncomingDamageLogs[entity].Where(l => l.Value.Modifier != null).ToList();

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

                var totalAbilitiesDone = parsedLogEntries.Count(l =>
                    l.Effect.EffectType == EffectType.Event && l.Effect.EffectId == _7_0LogParsing.AbilityActivateId);

                var interruptLogs = parsedLogEntries.Select((v, i) => new { value = v, index = i }).Where(l =>
                    l.value.Effect.EffectType == EffectType.Event && l.index != 0 &&
                    l.value.Effect.EffectId == _7_0LogParsing.InterruptCombatId &&
                    abilityIdsThatCanInterrupt.Contains(parsedLogEntries.ElementAt(l.index - 1).AbilityId));

                var mycleanseLogs = parsedLogEntries.Where(l => _cleanseAbilityIds.Contains(l.AbilityId)).Where(l => cleanseLogs.Any(t => t.LogLineNumber - l.LogLineNumber < 4 && t.LogLineNumber - l.LogLineNumber > 0));
                var myCleanseSpeeds = mycleanseLogs.Select(cl => GetSpeedFromLog(cl, cleanseLogs));
                var averageCleansespeed = myCleanseSpeeds.Any() ? myCleanseSpeeds.Average() : 0;

                var totalHealingReceived = combat.IncomingHealingLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived =
                    combat.IncomingHealingLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.MitigatedDblValue);

                var sheildingLogs = logEntries.Where(l => l.Value.Modifier is { ValueType: DamageType.shield });

                var enumerable = sheildingLogs as ParsedLogEntry[] ?? sheildingLogs.ToArray();
                var totalSheildingDone = enumerable.Length == 0 ? 0 : enumerable.Sum(l => l.Value.Modifier.DblValue);

                Dictionary<string, double> parriedAttackSums = CalculateEstimatedAvoidedDamage(combat, entity);

                combat.AverageCleanseSpeed[entity] = averageCleansespeed;
                combat.TotalInterrupts[entity] = interruptLogs.Count();
                combat.TotalCleanses[entity] = mycleanseLogs.Count();
                combat.TotalThreat[entity] = parsedLogEntries.Sum(l => l.Threat);
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

            if ((combat.DurationSeconds % 50) == 0 || !combat.HasBurstValues())
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

        private static double GetSpeedFromLog(ParsedLogEntry cl, IEnumerable<ParsedLogEntry> effectRemoveLogs)
        {
            var removedEffectLog = effectRemoveLogs.FirstOrDefault(l => l.LogLineNumber > cl.LogLineNumber);
            var cleanseTime = removedEffectLog.TimeStamp;
            var effectInQuestion = removedEffectLog.Effect.EffectId;
            var modifiersForCleansedEffect = CombatLogStateBuilder.CurrentState.Modifiers[effectInQuestion];
            var orderedModifiers = modifiersForCleansedEffect.Values.OrderBy(l => l.StartTime);
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
        private static Dictionary<Entity, List<DateTime>> GetTimestampsOfAbilitiesOnPlayers(List<ParsedLogEntry> abilityActivateLogs)
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
