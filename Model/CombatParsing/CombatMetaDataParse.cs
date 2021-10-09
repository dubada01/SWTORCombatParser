using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public static class CombatMetaDataParse
    {
        public static void PopulateMetaData(ref Combat combatToPopulate)
        {
            foreach (var participant in combatToPopulate.CharacterParticipants)
            {
                var combat = combatToPopulate;
                var combatDurationMs = (combatToPopulate.EndTime - combatToPopulate.StartTime).TotalMilliseconds;

                var logsInScope = combatToPopulate.Logs[participant];

                var outgoingLogs = logsInScope.Where(log => log.Source == participant).ToList();
                var incomingLogs = logsInScope.Where(log => log.Target == participant).ToList();

                combatToPopulate.OutgoingDamageLogs[participant] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combatToPopulate.OutgoingHealingLogs[participant] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

                combatToPopulate.IncomingDamageLogs[participant] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combatToPopulate.IncomingHealingLogs[participant] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();
                combatToPopulate.IncomingDamageMitigatedLogs[participant] = combatToPopulate.IncomingDamageLogs[participant].Where(l => l.Value.Modifier != null).ToList();

                var times = GetTimeBelow100Percent(combatToPopulate.IncomingHealingLogs[participant], combatToPopulate.IncomingDamageLogs[participant], combatToPopulate.StartTime, combatToPopulate.EndTime);
                combatToPopulate.TimeSpentBelowFullHealth[participant] = times.Sum(t => t.TotalSeconds);
                var totalHealing = combatToPopulate.OutgoingHealingLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveHealing = combatToPopulate.OutgoingHealingLogs[participant].Sum(l => l.Value.EffectiveDblValue);

                var totalDamage = combatToPopulate.OutgoingDamageLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveDamage = combatToPopulate.OutgoingDamageLogs[participant].Sum(l => l.Value.EffectiveDblValue);
                var currentFocusTarget = combatToPopulate.ParentEncounter?.BossNames;
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

                    totalDamage = combatToPopulate.OutgoingDamageLogs[participant].Where(d => !bosses.Contains(d.Target.Name)).Sum(l => l.Value.DblValue);
                    var focusDamageLogs = combatToPopulate.OutgoingDamageLogs[participant].Where(d => bosses.Contains(d.Target.Name));
                    var allFocusDamage = focusDamageLogs.Sum(l => l.Value.DblValue);
                    var allEffectiveFocusDamage = focusDamageLogs.Sum(l => l.Value.EffectiveDblValue);
                    combatToPopulate.TotalFocusDamage[participant] = allFocusDamage;
                    combatToPopulate.TotalEffectiveFocusDamage[participant] = allEffectiveFocusDamage;
                }
                else
                {
                    combatToPopulate.TotalFocusDamage[participant] = 0;
                    combatToPopulate.TotalEffectiveFocusDamage[participant] = 0;
                }

                var totalAbilitiesDone = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").Count();

                var interruptLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityInterrupt");

                var totalHealingReceived = combatToPopulate.IncomingHealingLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived = combatToPopulate.IncomingHealingLogs[participant].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combatToPopulate.IncomingDamageLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combatToPopulate.IncomingDamageLogs[participant].Sum(l => l.Value.EffectiveDblValue);

                var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.ValueType == DamageType.shield);

                var totalSheildingDone = sheildingLogs.Count() == 0 ? 0 : sheildingLogs.Sum(l => l.Value.Modifier.DblValue);

                Dictionary<string, double> _parriedAttackSums = CalculateEstimatedAvoidedDamage(combatToPopulate, participant);

                combatToPopulate.TotalInterrupts[participant] = interruptLogs.Count();
                combatToPopulate.TotalThreat[participant] = outgoingLogs.Sum(l => l.Threat);
                combatToPopulate.MaxDamage[participant] = combatToPopulate.OutgoingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingDamageLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxEffectiveDamage[participant] = combatToPopulate.OutgoingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingDamageLogs[participant].Max(l => l.Value.EffectiveDblValue);
                combatToPopulate.MaxHeal[participant] = combatToPopulate.OutgoingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingHealingLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxEffectiveHeal[participant] = combatToPopulate.OutgoingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingHealingLogs[participant].Max(l => l.Value.EffectiveDblValue);
                combatToPopulate.TotalFluffDamage[participant] = totalDamage;
                combatToPopulate.TotalEffectiveFluffDamage[participant] = totalEffectiveDamage;
                combatToPopulate.TotalTankSheilding[participant] = totalSheildingDone;
                combatToPopulate.TotalEstimatedAvoidedDamage[participant] = _parriedAttackSums.Sum(kvp => kvp.Value);
                combatToPopulate.TotalSheildAndAbsorb[participant] = combatToPopulate.IncomingDamageMitigatedLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingDamageMitigatedLogs[participant].Sum(l => l.Value.Modifier.EffectiveDblValue);
                combatToPopulate.TotalAbilites[participant] = totalAbilitiesDone;
                combatToPopulate.TotalHealing[participant] = totalHealing;
                combatToPopulate.TotalEffectiveHealing[participant] = totalEffectiveHealing;
                combatToPopulate.TotalDamageTaken[participant] = totalDamageTaken + combatToPopulate.TotalEstimatedAvoidedDamage[participant];
                combatToPopulate.TotalEffectiveDamageTaken[participant] = totalEffectiveDamageTaken;
                combatToPopulate.TotalHealingReceived[participant] = totalHealingReceived;
                combatToPopulate.TotalEffectiveHealingReceived[participant] = totalEffectiveHealingReceived;
                combatToPopulate.MaxIncomingDamage[participant] = combatToPopulate.IncomingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingDamageLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxEffectiveIncomingDamage[participant] = combatToPopulate.IncomingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingDamageLogs[participant].Max(l => l.Value.EffectiveDblValue);
                combatToPopulate.MaxIncomingHeal[participant] = combatToPopulate.IncomingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingHealingLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxIncomingEffectiveHeal[participant] = combatToPopulate.IncomingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingHealingLogs[participant].Max(l => l.Value.EffectiveDblValue);

            }
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
