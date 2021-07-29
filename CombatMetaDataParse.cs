﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public static class CombatMetaDataParse
    {
        public static void PopulateMetaData(ref Combat combatToPopulate)
        {
            var combatDurationMs = (combatToPopulate.EndTime - combatToPopulate.StartTime).TotalMilliseconds;

            var outgoingLogs = combatToPopulate.Logs.Where(log=>log.Source.IsPlayer).ToList();
            var incomingLogs = combatToPopulate.Logs.Where(log => log.Target.IsPlayer).ToList();

            combatToPopulate.OutgoingDamageLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
            combatToPopulate.OutgoingHealingLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

            combatToPopulate.IncomingDamageLogs = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
            combatToPopulate.IncomingHealingLogs = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

            var totalHealing = combatToPopulate.OutgoingHealingLogs.Sum(l => l.Value.DblValue);
            var totalEffectiveHealing = combatToPopulate.OutgoingHealingLogs.Sum(l => l.Threat * 2);

            var totalDamage = combatToPopulate.OutgoingDamageLogs.Sum(l => l.Value.DblValue);

            var totalAbilitiesDone = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").Count();

            var totalHealingReceived = combatToPopulate.IncomingHealingLogs.Sum(l => l.Value.DblValue);
            var totalEffectiveHealingReceived = combatToPopulate.IncomingHealingLogs.Sum(l => l.Threat * 2);

            var totalDamageTaken = combatToPopulate.IncomingDamageLogs.Sum(l => l.Value.DblValue - (l.Value.Modifier?.DblValue)??0);

            var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.DamageType == DamageType.shield);

            var totalSheildingDone = sheildingLogs.Count() == 0 ? 0: sheildingLogs.Sum(l => l.Value.Modifier.DblValue);



            combatToPopulate.MaxDamage = combatToPopulate.OutgoingDamageLogs.Count == 0 ? 0: combatToPopulate.OutgoingDamageLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxHeal = combatToPopulate.OutgoingHealingLogs.Count == 0 ? 0: combatToPopulate.OutgoingHealingLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxEffectiveHeal = combatToPopulate.OutgoingHealingLogs.Count == 0 ? 0 : combatToPopulate.OutgoingHealingLogs.Max(l => l.Threat*2);
            combatToPopulate.TotalDamage = totalDamage;
            combatToPopulate.TotalSheilding = totalSheildingDone;
            combatToPopulate.TotalAbilites = totalAbilitiesDone;
            combatToPopulate.TotalHealing = totalHealing;
            combatToPopulate.TotalEffectiveHealing = totalEffectiveHealing;
            combatToPopulate.TotalDamageTaken = totalDamageTaken;
            combatToPopulate.TotalHealingReceived = totalHealingReceived;
            combatToPopulate.TotalEffectiveHealingReceived = totalEffectiveHealingReceived;
            combatToPopulate.MaxIncomingDamage = combatToPopulate.IncomingDamageLogs.Count == 0 ? 0: combatToPopulate.IncomingDamageLogs.Max(l => l.Value.DblValue - (l.Value.Modifier?.DblValue) ?? 0);
            combatToPopulate.MaxIncomingHeal = combatToPopulate.IncomingHealingLogs.Count == 0 ? 0 : combatToPopulate.IncomingHealingLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxIncomingEffectiveHeal = combatToPopulate.IncomingHealingLogs.Count == 0 ? 0 : combatToPopulate.IncomingHealingLogs.Max(l => l.Threat * 2);


        }
        public static Dictionary<string,double> GetAverage(Dictionary<string,List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        {
            var returnDict = new Dictionary<string, double>();
            foreach(var kvp in combatMetaData)
            {
                if (!checkEffective)
                    returnDict[kvp.Key] = kvp.Value.Average(v => v.Value.DblValue);
                else
                    returnDict[kvp.Key] = kvp.Value.Average(v => v.Threat * 2);
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
                    returnDict[kvp.Key] = kvp.Value.Max(v => v.Threat * 2);
            }
            return returnDict;
        }
        public static Dictionary<string, double> GetSum(Dictionary<string, List<ParsedLogEntry>> combatMetaData, bool checkEffective = false)
        {
            var returnDict = new Dictionary<string, double>();
            foreach (var kvp in combatMetaData)
            {
                if(!checkEffective)
                    returnDict[kvp.Key] = kvp.Value.Sum(v => v.Value.DblValue);
                else
                    returnDict[kvp.Key] = kvp.Value.Sum(v => v.Threat*2);
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
