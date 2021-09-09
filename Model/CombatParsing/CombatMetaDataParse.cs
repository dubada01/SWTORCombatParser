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
            foreach(var participant in combatToPopulate.CharacterParticipants)
            {
                var combat = combatToPopulate;
                var combatDurationMs = (combatToPopulate.EndTime - combatToPopulate.StartTime).TotalMilliseconds;

                var logsInScope = combatToPopulate.Logs[participant];

                var outgoingLogs = logsInScope.Where(log => log.Source == participant).ToList();
                var incomingLogs = logsInScope.Where(log => log.Target == participant).ToList();

               // PopulateCompanionData(combatToPopulate);


                combatToPopulate.OutgoingDamageLogs[participant] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combatToPopulate.OutgoingHealingLogs[participant] = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

                combatToPopulate.IncomingDamageLogs[participant] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
                combatToPopulate.IncomingHealingLogs[participant] = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();
                var test = combatToPopulate.IncomingDamageLogs[participant].Where(l => l.Value.Modifier != null).ToList();
                combatToPopulate.IncomingSheildedLogs[participant] = combatToPopulate.IncomingDamageLogs[participant].Where(l => l.Value.Modifier != null && l.Value.Modifier.ValueType == DamageType.absorbed).ToList();

                var totalHealing = combatToPopulate.OutgoingHealingLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveHealing = combatToPopulate.OutgoingHealingLogs[participant].Sum(l => l.Value.EffectiveDblValue);

                var totalDamage = combatToPopulate.OutgoingDamageLogs[participant].Sum(l => l.Value.DblValue);

                var currentFocusTarget = combatToPopulate.ParentEncounter?.BossNames;
                if (currentFocusTarget != null && currentFocusTarget.Count > 0)
                {
                    var bosses = currentFocusTarget.SelectMany(boss => {
                        if (!boss.Contains("~?~"))
                            return new List<string> { boss };
                        else
                        {
                            var names = boss.Split("~?~", StringSplitOptions.None)[1];
                            return new List<string>(names.Split('|'));
                        }
                    }).ToList();

                    totalDamage = combatToPopulate.OutgoingDamageLogs[participant].Where(d => !bosses.Contains(d.Target.Name)).Sum(l => l.Value.DblValue);
                    var focusDamageLogs = combatToPopulate.OutgoingDamageLogs[participant].Where(d => currentFocusTarget.Any(boss => boss.Split('|').Contains(d.Target.Name)));
                    var allFocusDamage = focusDamageLogs.Sum(l => l.Value.DblValue);
                    combatToPopulate.TotalFocusDamage[participant] = allFocusDamage;
                }
                else
                {
                    combatToPopulate.TotalFocusDamage[participant] = 0;
                }

                var totalAbilitiesDone = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").Count();

                var totalHealingReceived = combatToPopulate.IncomingHealingLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveHealingReceived = combatToPopulate.IncomingHealingLogs[participant].Sum(l => l.Value.EffectiveDblValue);

                var totalDamageTaken = combatToPopulate.IncomingDamageLogs[participant].Sum(l => l.Value.DblValue);
                var totalEffectiveDamageTaken = combatToPopulate.IncomingDamageLogs[participant].Sum(l => l.Value.EffectiveDblValue);

                var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.ValueType == DamageType.shield);

                var totalSheildingDone = sheildingLogs.Count() == 0 ? 0 : sheildingLogs.Sum(l => l.Value.Modifier.DblValue);


                combatToPopulate.TotalThreat[participant] = outgoingLogs.Sum(l => l.Threat);
                combatToPopulate.MaxDamage[participant] = combatToPopulate.OutgoingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingDamageLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxHeal[participant] = combatToPopulate.OutgoingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingHealingLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxEffectiveHeal[participant] = combatToPopulate.OutgoingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.OutgoingHealingLogs[participant].Max(l => l.Value.EffectiveDblValue);
                combatToPopulate.TotalFluffDamage[participant] = totalDamage;
                combatToPopulate.TotalSheilding[participant] = totalSheildingDone;
                combatToPopulate.TotalAbilites[participant] = totalAbilitiesDone;
                combatToPopulate.TotalHealing[participant] = totalHealing;
                combatToPopulate.TotalEffectiveHealing[participant] = totalEffectiveHealing;
                combatToPopulate.TotalDamageTaken[participant] = totalDamageTaken;
                combatToPopulate.TotalEffectiveDamageTaken[participant] = totalEffectiveDamageTaken;
                combatToPopulate.TotalHealingReceived[participant] = totalHealingReceived;
                combatToPopulate.TotalEffectiveHealingReceived[participant] = totalEffectiveHealingReceived;
                combatToPopulate.MaxIncomingDamage[participant] = combatToPopulate.IncomingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingDamageLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxEffectiveIncomingDamage[participant] = combatToPopulate.IncomingDamageLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingDamageLogs[participant].Max(l => l.Value.EffectiveDblValue);
                combatToPopulate.MaxIncomingHeal[participant] = combatToPopulate.IncomingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingHealingLogs[participant].Max(l => l.Value.DblValue);
                combatToPopulate.MaxIncomingEffectiveHeal[participant] = combatToPopulate.IncomingHealingLogs[participant].Count == 0 ? 0 : combatToPopulate.IncomingHealingLogs[participant].Max(l => l.Value.EffectiveDblValue);

            }
        }
        //private static void PopulateCompanionData(Combat combatToUpdate)
        //{
        //    var companionOutgoing = combatToUpdate.Logs.Where(log => log.Source.IsCompanion);
            
        //    var companionDamageLogs = companionOutgoing.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
        //    var companionHealLogs = companionOutgoing.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

        //    combatToUpdate.TotalCompanionDamage = companionDamageLogs.Sum(l => l.Value.DblValue);
        //    combatToUpdate.TotalCompanionHealing = companionHealLogs.Sum(l => l.Value.DblValue);
        //    combatToUpdate.TotalEffectiveCompanionHealing = companionHealLogs.Sum(l => l.Value.EffectiveDblValue);

        //}
        //public static Dictionary<string,double> GetAverage(Dictionary<string,List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        //{
        //    var returnDict = new Dictionary<string, double>();
        //    foreach(var kvp in combatMetaData)
        //    {
        //        if (!checkEffective)
        //            returnDict[kvp.Key] = kvp.Value.Average(v => v.Value.DblValue);
        //        else
        //            returnDict[kvp.Key] = kvp.Value.Average(v => v.Value.EffectiveDblValue);
        //    }
        //    return returnDict;
        //}
        //public static Dictionary<string, double> GetMax(Dictionary<string, List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        //{
        //    var returnDict = new Dictionary<string, double>();
        //    foreach (var kvp in combatMetaData)
        //    {
        //        if (!checkEffective)
        //            returnDict[kvp.Key] = kvp.Value.Max(v => v.Value.DblValue);
        //        else
        //            returnDict[kvp.Key] = kvp.Value.Max(v => v.Value.EffectiveDblValue);
        //    }
        //    return returnDict;
        //}
        //public static Dictionary<string, double> GetSum(Dictionary<string, List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        //{
        //    var returnDict = new Dictionary<string, double>();
        //    foreach (var kvp in combatMetaData)
        //    {
        //        if(!checkEffective)
        //            returnDict[kvp.Key] = kvp.Value.Sum(v => v.Value.DblValue);
        //        else
        //            returnDict[kvp.Key] = kvp.Value.Sum(v => v.Value.EffectiveDblValue);
        //    }
        //    return returnDict;
        //}
        //public static Dictionary<string, double> Getcount(Dictionary<string, List<ParsedLogEntry>> combatMetaData)
        //{
        //    var returnDict = new Dictionary<string, double>();
        //    foreach (var kvp in combatMetaData)
        //    {
        //        returnDict[kvp.Key] = kvp.Value.Count();
        //    }
        //    return returnDict;
        //}
        //public static Dictionary<string, double> GetCritPercent(Dictionary<string, List<ParsedLogEntry>> combatMetaData)
        //{
        //    var returnDict = new Dictionary<string, double>();
        //    foreach (var kvp in combatMetaData)
        //    {
        //        returnDict[kvp.Key] = kvp.Value.Count(v => v.Value.WasCrit) / kvp.Value.Count();
        //    }
        //    return returnDict;
        //}
        //public static Dictionary<string, double> GetEffectiveHealsPercent(Dictionary<string, List<ParsedLogEntry>> combatMetaData)
        //{
        //    var sumEffective = GetSum(combatMetaData, true);
        //    var sumTotal = GetSum(combatMetaData);

        //    var returnDict = new Dictionary<string, double>();
        //    foreach (var kvp in combatMetaData)
        //    {
        //        returnDict[kvp.Key] = sumEffective[kvp.Key] / sumTotal[kvp.Key];
        //    }
        //    return returnDict;
        //}
    }
}
