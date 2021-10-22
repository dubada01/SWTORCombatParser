using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public static class CombatMetaDataParse
    {
        public static void PopulateMetaData(Combat combatToPopulate)
        {
            var combat = combatToPopulate;
            //Parallel.ForEach(combatToPopulate.AllEntities, entitiy =>
            foreach (var entitiy in combatToPopulate.AllEntities)
            {

                var combatDurationMs = (combat.EndTime - combat.StartTime).TotalMilliseconds;

                var logsInScope = combat.GetLogsInvolvingEntity(entitiy);

                var outgoingLogs = logsInScope.Where(log => log.Source == entitiy).ToList();
                var incomingLogs = logsInScope.Where(log => log.Target == entitiy).ToList();

                combat.OutgoingDamageLogs[entitiy] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combat.OutgoingHealingLogs[entitiy] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

                combat.IncomingDamageLogs[entitiy] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combat.IncomingHealingLogs[entitiy] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();
                combat.IncomingDamageMitigatedLogs[entitiy] = combat.IncomingDamageLogs[entitiy].Where(l => l.Value.Modifier != null).ToList();

                var times = GetTimeBelow100Percent(combat.IncomingHealingLogs[entitiy], combat.IncomingDamageLogs[entitiy], combat.StartTime, combat.EndTime);
                combat.TimeSpentBelowFullHealth[entitiy] = times.Sum(t => t.TotalSeconds);
                var totalHealing = combat.OutgoingHealingLogs[entitiy].Sum(l => l.Value.DblValue);
                var totalEffectiveHealing = combat.OutgoingHealingLogs[entitiy].Sum(l => l.Value.EffectiveDblValue);

                var totalDamage = combat.OutgoingDamageLogs[entitiy].Sum(l => l.Value.DblValue);
                var totalEffectiveDamage = combat.OutgoingDamageLogs[entitiy].Sum(l => l.Value.EffectiveDblValue);
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

                    totalDamage = combat.OutgoingDamageLogs[entitiy].Where(d => !bosses.Contains(d.Target.Name)).Sum(l => l.Value.DblValue);
                    totalEffectiveDamage = combat.OutgoingDamageLogs[entitiy].Where(d => !bosses.Contains(d.Target.Name)).Sum(l => l.Value.EffectiveDblValue);
                    var focusDamageLogs = combat.OutgoingDamageLogs[entitiy].Where(d => bosses.Contains(d.Target.Name));
                    var allFocusDamage = focusDamageLogs.Sum(l => l.Value.DblValue);
                    var allEffectiveFocusDamage = focusDamageLogs.Sum(l => l.Value.EffectiveDblValue);
                    combat.TotalFocusDamage[entitiy] = allFocusDamage;
                    combat.TotalEffectiveFocusDamage[entitiy] = allEffectiveFocusDamage;
                }
                else
                {
                    combat.TotalFocusDamage[entitiy] = 0;
                    combat.TotalEffectiveFocusDamage[entitiy] = 0;
                }

                var totalAbilitiesDone = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").Count();

                var interruptLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityInterrupt");

                var totalHealingReceived = combat.IncomingHealingLogs[entitiy].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived = combat.IncomingHealingLogs[entitiy].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combat.IncomingDamageLogs[entitiy].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combat.IncomingDamageLogs[entitiy].Sum(l => l.Value.EffectiveDblValue);

                var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.ValueType == DamageType.shield);

                var totalSheildingDone = sheildingLogs.Count() == 0 ? 0 : sheildingLogs.Sum(l => l.Value.Modifier.DblValue);

                Dictionary<string, double> _parriedAttackSums = CalculateEstimatedAvoidedDamage(combat, entitiy);

                combat.TotalInterrupts[entitiy] = interruptLogs.Count();
                combat.TotalThreat[entitiy] = outgoingLogs.Sum(l => l.Threat);
                combat.MaxDamage[entitiy] = combat.OutgoingDamageLogs[entitiy].Count == 0 ? 0 : combat.OutgoingDamageLogs[entitiy].Max(l => l.Value.DblValue);
                combat.MaxEffectiveDamage[entitiy] = combat.OutgoingDamageLogs[entitiy].Count == 0 ? 0 : combat.OutgoingDamageLogs[entitiy].Max(l => l.Value.EffectiveDblValue);
                combat.MaxHeal[entitiy] = combat.OutgoingHealingLogs[entitiy].Count == 0 ? 0 : combat.OutgoingHealingLogs[entitiy].Max(l => l.Value.DblValue);
                combat.MaxEffectiveHeal[entitiy] = combat.OutgoingHealingLogs[entitiy].Count == 0 ? 0 : combat.OutgoingHealingLogs[entitiy].Max(l => l.Value.EffectiveDblValue);
                combat.TotalFluffDamage[entitiy] = totalDamage;
                combat.TotalEffectiveFluffDamage[entitiy] = totalEffectiveDamage;
                combat.TotalTankSheilding[entitiy] = totalSheildingDone;
                combat.TotalEstimatedAvoidedDamage[entitiy] = _parriedAttackSums.Sum(kvp => kvp.Value);
                combat.TotalSheildAndAbsorb[entitiy] = combat.IncomingDamageMitigatedLogs[entitiy].Count == 0 ? 0 : combat.IncomingDamageMitigatedLogs[entitiy].Sum(l => l.Value.Modifier.EffectiveDblValue);
                combat.TotalAbilites[entitiy] = totalAbilitiesDone;
                combat.TotalHealing[entitiy] = totalHealing;
                combat.TotalEffectiveHealing[entitiy] = totalEffectiveHealing;
                combat.TotalDamageTaken[entitiy] = totalDamageTaken + combat.TotalEstimatedAvoidedDamage[entitiy];
                combat.TotalEffectiveDamageTaken[entitiy] = totalEffectiveDamageTaken;
                combat.TotalHealingReceived[entitiy] = totalHealingReceived;
                combat.TotalEffectiveHealingReceived[entitiy] = totalEffectiveHealingReceived;
                combat.MaxIncomingDamage[entitiy] = combat.IncomingDamageLogs[entitiy].Count == 0 ? 0 : combat.IncomingDamageLogs[entitiy].Max(l => l.Value.DblValue);
                combat.MaxEffectiveIncomingDamage[entitiy] = combat.IncomingDamageLogs[entitiy].Count == 0 ? 0 : combat.IncomingDamageLogs[entitiy].Max(l => l.Value.EffectiveDblValue);
                combat.MaxIncomingHeal[entitiy] = combat.IncomingHealingLogs[entitiy].Count == 0 ? 0 : combat.IncomingHealingLogs[entitiy].Max(l => l.Value.DblValue);
                combat.MaxIncomingEffectiveHeal[entitiy] = combat.IncomingHealingLogs[entitiy].Count == 0 ? 0 : combat.IncomingHealingLogs[entitiy].Max(l => l.Value.EffectiveDblValue);
            }
           // });
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

        public static List<TimeSpan> GetTimeBelow100Percent(List<ParsedLogEntry> incomingHeals, List<ParsedLogEntry> incomingDamage, DateTime combatStart, DateTime combatEnd)
        {
            
            var times = new List<TimeSpan>();
            if (incomingDamage.Count == 0 && incomingHeals.Count == 0)
                return times;
            
            var firstIncomingDamageTime = incomingDamage.FirstOrDefault(id => id.Value.EffectiveDblValue > 0);
            var timeStarted = firstIncomingDamageTime != null ? firstIncomingDamageTime.TimeStamp : combatStart;

            bool isDamaged = false;
            var firstDamageTime = GetFirstDamageTime(incomingDamage);
            var currentDamageTime = firstDamageTime;
            if (currentDamageTime > DateTime.MinValue)
                isDamaged = true;
            var firstEffectiveRecivedTime = GetFirstEffectiveHealTime(incomingHeals);
            if (firstEffectiveRecivedTime < firstDamageTime || (firstDamageTime == DateTime.MinValue && firstEffectiveRecivedTime!=DateTime.MinValue))
            {
                var timeAtFullHP = GetNextLessThanTotallyEffectiveHeal(currentDamageTime, incomingHeals);
                if (timeAtFullHP == DateTime.MinValue)
                {
                    times.Add(combatEnd - timeStarted);
                    return times;
                }
                else
                    times.Add(timeAtFullHP - timeStarted);
            }
            
            foreach(var log in incomingDamage)
            {
                if (isDamaged)
                {
                    var timeAtFullHP = GetNextLessThanTotallyEffectiveHeal(currentDamageTime, incomingHeals);
                    if (timeAtFullHP == DateTime.MinValue)
                    { 
                        times.Add(combatEnd - currentDamageTime);
                        break;
                    }
                    else
                        times.Add(timeAtFullHP - currentDamageTime);
                    currentDamageTime = timeAtFullHP;
                    isDamaged = false;
                }
                if (currentDamageTime < log.TimeStamp && log.Value.EffectiveDblValue > 0)
                { 
                    currentDamageTime = log.TimeStamp;
                    isDamaged = true;
                }
                
                

            }

            return times;
        }
        private static DateTime GetNextLessThanTotallyEffectiveHeal(DateTime damageTime, List<ParsedLogEntry> incomingHeals)
        {
            var logsInScope = incomingHeals.Where(t => t.TimeStamp > damageTime);
            var logAtFullHP = logsInScope.FirstOrDefault(l => (l.Value.DblValue - l.Value.EffectiveDblValue) > 1);
            if (logAtFullHP == null)
                return DateTime.MinValue;
            return logAtFullHP.TimeStamp;
        }
        private static DateTime GetFirstEffectiveHealTime(List<ParsedLogEntry> incomingHeals)
        {
            if (incomingHeals.Count == 0)
                return DateTime.MinValue;
            var effectiveLog = incomingHeals.FirstOrDefault(t => t.Value.EffectiveDblValue > 0);
            return effectiveLog != null ? effectiveLog.TimeStamp:DateTime.MinValue;
        }
        private static DateTime GetFirstDamageTime(List<ParsedLogEntry> incomingdamage)
        {
            if (incomingdamage.Count == 0)
                return DateTime.MinValue;
            var firstDamageTime = incomingdamage.FirstOrDefault(d => d.Value.EffectiveDblValue > 0);
            return firstDamageTime != null ? firstDamageTime.TimeStamp : DateTime.MinValue;
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
