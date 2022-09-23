using MathNet.Numerics.Statistics;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public static class CombatMetaDataParse
    {
        private static List<string> interruptAbilityNames = new List<string> { "Distraction", "Mind Snap","Force Kick","Disruption", "Force Leap","Force Charge","Riot Strike","Disabling Shot","Jolt" };
        private static List<string> stunAbilityNames = new List<string> {"Electro Dart","Debilitate","Maim","Low Slash","Electrocute","Force Choke","Cryo Grenade","Dirty Kick","Force Stun","Force Stasis" };

        private static List<string> abilitiesThatCanInterrupt => interruptAbilityNames.Concat(stunAbilityNames).ToList();
        public static void PopulateMetaData(Combat combatToPopulate)
        {
            var combat = combatToPopulate;
            //Parallel.ForEach(combatToPopulate.AllEntities, entitiy =>
            foreach (var entity in combatToPopulate.AllEntities)
            {

                var combatDurationMs = (combat.EndTime - combat.StartTime).TotalMilliseconds;

                var logsInScope = combat.GetLogsInvolvingEntity(entity);

                var outgoingLogs = logsInScope.Where(log => log.Source == entity).ToList();
                var incomingLogs = logsInScope.Where(log => log.Target == entity).ToList();

                combat.OutgoingDamageLogs[entity] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combat.OutgoingHealingLogs[entity] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();
                combat.AbilitiesActivated[entity] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").ToList();
                combat.IncomingDamageLogs[entity] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combat.IncomingHealingLogs[entity] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

                var bigDamageTimestamps = GetTimestampOfBigHits(combat.IncomingDamageLogs[entity]);
                combat.BigDamageTimestamps[entity] = bigDamageTimestamps;

                combat.IncomingDamageMitigatedLogs[entity] = combat.IncomingDamageLogs[entity].Where(l => l.Value.Modifier != null).ToList();

                var totalHealing = combat.OutgoingHealingLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveHealing = combat.OutgoingHealingLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var totalDamage = combat.OutgoingDamageLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveDamage = combat.OutgoingDamageLogs[entity].Sum(l => l.Value.EffectiveDblValue);
                var currentFocusTarget = combat.ParentEncounter?.BossNames;
                if (currentFocusTarget != null && currentFocusTarget.Count > 0)
                {
                    var bosses = currentFocusTarget.SelectMany(boss =>
                    {
                        if (!boss.Contains("~?~"))
                            return new List<string> { boss };
                        else
                        {
                            var names = boss.Split("~?~", StringSplitOptions.None)[1];
                            return new List<string>(names.Split('|'));
                        }
                    }).ToList();

                    totalDamage = combat.OutgoingDamageLogs[entity].Where(d => !bosses.Contains(d.Target.Name)).Sum(l => l.Value.DblValue);
                    totalEffectiveDamage = combat.OutgoingDamageLogs[entity].Where(d => !bosses.Contains(d.Target.Name)).Sum(l => l.Value.EffectiveDblValue);
                    var focusDamageLogs = combat.OutgoingDamageLogs[entity].Where(d => bosses.Contains(d.Target.Name));
                    var allFocusDamage = focusDamageLogs.Sum(l => l.Value.DblValue);
                    var allEffectiveFocusDamage = focusDamageLogs.Sum(l => l.Value.EffectiveDblValue);
                    combat.TotalFocusDamage[entity] = allFocusDamage;
                    combat.TotalEffectiveFocusDamage[entity] = allEffectiveFocusDamage;
                }
                else
                {
                    combat.TotalFocusDamage[entity] = 0;
                    combat.TotalEffectiveFocusDamage[entity] = 0;
                }

                var totalAbilitiesDone = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").Count();

                var interruptLogs = outgoingLogs.Select((v,i)=>new {value=v,index=i}).Where(l => l.value.Effect.EffectType == EffectType.Event && l.index != 0 && l.value.Effect.EffectName == "AbilityInterrupt" && abilitiesThatCanInterrupt.Contains(outgoingLogs[l.index-1].Ability));

                var totalHealingReceived = combat.IncomingHealingLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived = combat.IncomingHealingLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.ValueType == DamageType.shield);

                var totalSheildingDone = sheildingLogs.Count() == 0 ? 0 : sheildingLogs.Sum(l => l.Value.Modifier.DblValue);

                Dictionary<string, double> _parriedAttackSums = CalculateEstimatedAvoidedDamage(combat, entity);

                combat.TotalInterrupts[entity] = interruptLogs.Count();
                combat.TotalThreat[entity] = outgoingLogs.Sum(l => (long)l.Threat);
                combat.MaxDamage[entity] = combat.OutgoingDamageLogs[entity].Count == 0 ? 0 : combat.OutgoingDamageLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxEffectiveDamage[entity] = combat.OutgoingDamageLogs[entity].Count == 0 ? 0 : combat.OutgoingDamageLogs[entity].Max(l => l.Value.EffectiveDblValue);
                combat.MaxHeal[entity] = combat.OutgoingHealingLogs[entity].Count == 0 ? 0 : combat.OutgoingHealingLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxEffectiveHeal[entity] = combat.OutgoingHealingLogs[entity].Count == 0 ? 0 : combat.OutgoingHealingLogs[entity].Max(l => l.Value.EffectiveDblValue);
                combat.TotalFluffDamage[entity] = totalDamage;
                combat.TotalEffectiveFluffDamage[entity] = totalEffectiveDamage;
                combat.TotalTankSheilding[entity] = totalSheildingDone;
                combat.TotalEstimatedAvoidedDamage[entity] = _parriedAttackSums.Sum(kvp => kvp.Value);
                combat.TotalSheildAndAbsorb[entity] = combat.IncomingDamageMitigatedLogs[entity].Count == 0 ? 0 : combat.IncomingDamageMitigatedLogs[entity].Sum(l => l.Value.Modifier.EffectiveDblValue);
                combat.TotalAbilites[entity] = totalAbilitiesDone;
                combat.TotalHealing[entity] = totalHealing;
                combat.TotalEffectiveHealing[entity] = totalEffectiveHealing;
                combat.TotalDamageTaken[entity] = totalDamageTaken + combat.TotalEstimatedAvoidedDamage[entity];
                combat.TotalEffectiveDamageTaken[entity] = totalEffectiveDamageTaken;
                combat.TotalHealingReceived[entity] = totalHealingReceived;
                combat.TotalEffectiveHealingReceived[entity] = totalEffectiveHealingReceived;
                combat.MaxIncomingDamage[entity] = combat.IncomingDamageLogs[entity].Count == 0 ? 0 : combat.IncomingDamageLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxEffectiveIncomingDamage[entity] = combat.IncomingDamageLogs[entity].Count == 0 ? 0 : combat.IncomingDamageLogs[entity].Max(l => l.Value.EffectiveDblValue);
                combat.MaxIncomingHeal[entity] = combat.IncomingHealingLogs[entity].Count == 0 ? 0 : combat.IncomingHealingLogs[entity].Max(l => l.Value.DblValue);
                combat.MaxIncomingEffectiveHeal[entity] = combat.IncomingHealingLogs[entity].Count == 0 ? 0 : combat.IncomingHealingLogs[entity].Max(l => l.Value.EffectiveDblValue);
            }
            if(((int)combat.DurationSeconds%50)==0 || !combat.HasBurstValues())
                combat.SetBurstValues();

            var healers = combat.CharacterParticipants.Where(p => CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(p, combat.EndTime).Role == DataStructures.Role.Healer);
            var tanks = combat.CharacterParticipants.Where(p => CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(p, combat.EndTime).Role == DataStructures.Role.Tank);

            foreach (var healer in healers)
            {
                var abilityActivateTimesOnTargets = GetTimestampsOfAbilitiesOnPlayers(combat.AbilitiesActivated[healer]);
                var reactionTimesToBigHigs = CalculateReactionToBigHits(combat.BigDamageTimestamps.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), abilityActivateTimesOnTargets);
                combat.AllDamageRecoveryTimes[healer] = reactionTimesToBigHigs;
                var reactionTimesToBigHigsOnTanks = CalculateReactionToBigHits(combat.BigDamageTimestamps.Where(kvp => tanks.Contains(kvp.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value), abilityActivateTimesOnTargets);
                combat.TankDamageRecoveryTimes[healer] = reactionTimesToBigHigsOnTanks;
            }
        }
        private static Dictionary<Entity, List<double>> CalculateReactionToBigHits(Dictionary<Entity,List<DateTime>> bigHitTimestamps, Dictionary<Entity, List<DateTime>> reactionTimeStamps)
        {
            var delays = new Dictionary<Entity, List<double>>();
            foreach(var target in bigHitTimestamps.Keys.Where(e=>e.IsCharacter))
            {
                if (!delays.ContainsKey(target))
                    delays[target] = new List<double>();
                if (!reactionTimeStamps.ContainsKey(target))
                    continue;
                var reactionsForTarget = reactionTimeStamps[target];
                foreach(var hit in bigHitTimestamps[target])
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
            for(var i = 0; i < values.Count; i++)
            {
                if (values[i] > comparison)
                    return values[i];
            }
            return null;
        }
        private static List<DateTime> GetTimestampOfBigHits(List<ParsedLogEntry> incomingDamage)
        {
            var timestamps = new List<DateTime>();
            if (!incomingDamage.Any())
                return timestamps;

            var threshold = incomingDamage.First().TargetInfo.MaxHP * 0.05;
            List<(DateTime,double)> oneSecondOfDamage = new List<(DateTime,double)> ();
            foreach(var damage in incomingDamage)
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
                    var totalDamageOverLastSecond = oneSecondOfDamage.Select(v=>v.Item2).Sum();
                    if(totalDamageOverLastSecond >= threshold)
                    {
                        timestamps.Add(damage.TimeStamp);
                        oneSecondOfDamage.Clear();
                    }
                }
            }
            return timestamps;
        }
        private static Dictionary<Entity,List<DateTime>> GetTimestampsOfAbilitiesOnPlayers(List<ParsedLogEntry> abilityActivateLogs)
        {
            var returnDict = new Dictionary<Entity, List<DateTime>>();
            foreach(var abilityActivation in abilityActivateLogs.Where(l=>l.Target.IsCharacter))
            {
                var target = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(abilityActivation.Source, abilityActivation.TimeStamp);
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
            Dictionary<string, double> _parriedAttackSums = new Dictionary<string, double>();
            var damageDone = combatToPopulate.GetIncomingDamageByAbility(participant);
            foreach (var mitigatedAttack in totallyMitigatedAttacks.Select(l => l.Ability).Distinct())
            {
                var numberOfParries = totallyMitigatedAttacks.Count(l => l.Ability == mitigatedAttack);
                var damageFromUnparriedAttacks = damageDone[mitigatedAttack].Select(v => v.Value.EffectiveDblValue).Where(v => v > 0);
                if (damageFromUnparriedAttacks.Count() == 0)
                    continue;
                var averageDamageFromUnparriedAttack = damageFromUnparriedAttacks.Mean() * numberOfParries;
                _parriedAttackSums[mitigatedAttack] = averageDamageFromUnparriedAttack;
            }

            return _parriedAttackSums;
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
                returnDict[kvp.Key] = kvp.Value.Count(v => v.Value.WasCrit) / kvp.Value.Count();
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
