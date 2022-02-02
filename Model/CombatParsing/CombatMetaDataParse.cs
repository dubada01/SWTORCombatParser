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
            foreach (var entity in combatToPopulate.AllEntities)
            {

                var combatDurationMs = (combat.EndTime - combat.StartTime).TotalMilliseconds;

                var logsInScope = combat.GetLogsInvolvingEntity(entity);

                var outgoingLogs = logsInScope.Where(log => log.Source == entity).ToList();
                var incomingLogs = logsInScope.Where(log => log.Target == entity).ToList();

                combat.OutgoingDamageLogs[entity] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combat.OutgoingHealingLogs[entity] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

                combat.IncomingDamageLogs[entity] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combat.IncomingHealingLogs[entity] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

                var damageIncomingForEachSecond = CalculateValueFromEachSecond(combat.IncomingDamageLogs[entity], (int)combatDurationMs / 1000, combat.StartTime);
                var healingIncomingForEachSecond = CalculateValuePerPlayerFromEachSecond(combat.IncomingHealingLogs[entity], (int)combatDurationMs / 1000, combat.StartTime);
                var recoveryTimes = CalculateAverageHealRecoveryTime(damageIncomingForEachSecond, healingIncomingForEachSecond);

                foreach (var healer in recoveryTimes.Keys)
                {
                    if (!combat.DamageRecoveryTimes.ContainsKey(healer))
                    {
                        combat.DamageRecoveryTimes[healer] = new Dictionary<Entity, List<double>>();
                    }
                    if (!combat.DamageRecoveryTimes[healer].ContainsKey(entity))
                    {
                        combat.DamageRecoveryTimes[healer][entity] = new List<double>();
                    }
                    combat.DamageRecoveryTimes[healer][entity] = recoveryTimes[healer];
                }


                combat.IncomingDamageMitigatedLogs[entity] = combat.IncomingDamageLogs[entity].Where(l => l.Value.Modifier != null).ToList();

                //var times = GetTimeBelow100Percent(combat.IncomingHealingLogs[entity], combat.IncomingDamageLogs[entity], combat.StartTime, combat.EndTime);
                //combat.TimeSpentBelowFullHealth[entity] = times.Sum(t => t.TotalSeconds);
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

                var interruptLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityInterrupt");

                var totalHealingReceived = combat.IncomingHealingLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived = combat.IncomingHealingLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combat.IncomingDamageLogs[entity].Sum(l => l.Value.EffectiveDblValue);

                var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.ValueType == DamageType.shield);

                var totalSheildingDone = sheildingLogs.Count() == 0 ? 0 : sheildingLogs.Sum(l => l.Value.Modifier.DblValue);

                Dictionary<string, double> _parriedAttackSums = CalculateEstimatedAvoidedDamage(combat, entity);

                combat.TotalInterrupts[entity] = interruptLogs.Count();
                combat.TotalThreat[entity] = outgoingLogs.Sum(l => l.Threat);
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
           // });
        }

        private static Dictionary<Entity,List<double>> CalculateAverageHealRecoveryTime(double[] damageIncomingForEachSecond, Dictionary<Entity,double[]> healingIncomingForEachSecond)
        {
            var returnDict = new Dictionary<Entity, List<double>>();
            foreach(var entity in healingIncomingForEachSecond.Keys)
            {
                var secondsTillRecovered = new List<double>();
                var healsList = healingIncomingForEachSecond[entity].ToList();
                for (var i = 0; i < damageIncomingForEachSecond.Length; i++)
                {
                    var damageDuringSecond = damageIncomingForEachSecond[i];
                    var healsFromNow = healsList.GetRange(i, healsList.Count - i);
                    var secondsUntilRecovered = 0d;
                    var currentHeals = 0d;
                    for (var hi = 0; hi < healsFromNow.Count; hi++)
                    {
                        if (damageDuringSecond == 0)
                        {
                            secondsUntilRecovered = double.NaN;
                            break;
                        }
                        if (currentHeals >= damageDuringSecond)
                            break;
                        currentHeals += healsFromNow[hi];
                        secondsUntilRecovered++;
                        if (hi == healsFromNow.Count - 1)
                            secondsUntilRecovered = damageIncomingForEachSecond.Length;
                    }
                    secondsTillRecovered.Add(secondsUntilRecovered);
                }
                returnDict[entity] = secondsTillRecovered;
            }

            return returnDict;
        }

        private static double[] CalculateValueFromEachSecond(List<ParsedLogEntry> parsedLogEntries, int combatNumberOfSeconds, DateTime combatStart)
        {
            var listOfSecondsLength = new double[combatNumberOfSeconds];

            for(var i = 0; i < listOfSecondsLength.Length-1; i++)
            {
                var damageDuringSecond = parsedLogEntries.Where(l => (l.TimeStamp - combatStart).TotalSeconds < i && (l.TimeStamp - combatStart).TotalSeconds >= i - 1);
                if (damageDuringSecond.Any())
                {
                    listOfSecondsLength[i] = damageDuringSecond.Sum(d => d.Value.EffectiveDblValue);
                }
            }
            return listOfSecondsLength;
        }
        private static Dictionary<Entity,double[]> CalculateValuePerPlayerFromEachSecond(List<ParsedLogEntry> parsedLogEntries, int combatNumberOfSeconds, DateTime combatStart)
        {
            var participants = parsedLogEntries.Select(l => l.Source).Where(e => e.IsCharacter).Distinct();
            var returnDict = participants.ToDictionary(e => e, e=>new double[combatNumberOfSeconds]);
            foreach(var player in participants)
            {
                var listOfSecondsLength = returnDict[player];

                for (var i = 0; i < listOfSecondsLength.Length - 1; i++)
                {
                    var valueduringSecond = parsedLogEntries.Where(l => (l.TimeStamp - combatStart).TotalSeconds < i && (l.TimeStamp - combatStart).TotalSeconds >= i - 1 && l.Source == player);
                    if (valueduringSecond.Any())
                    {
                        listOfSecondsLength[i] = valueduringSecond.Sum(d => d.Value.EffectiveDblValue);
                    }
                }
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
