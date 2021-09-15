using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.RaidInfos;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser
{
    public class Combat
    {
        public List<Entity> CharacterParticipants = new List<Entity>();
        public List<Entity> Targets = new List<Entity>();
        public DateTime StartTime;
        public DateTime EndTime;
        public string LogFileName => Logs.Where(l=>!string.IsNullOrEmpty(l.Value.First().LogName)).FirstOrDefault().Value.First().LogName;
        public double DurationMS => (EndTime - StartTime).TotalMilliseconds;
        public double DurationSeconds => DurationMS / 1000f;

        
        public EncounterInfo ParentEncounter;
        public string EncounterBossInfo;
        public bool IsEncounterBoss;

        public Dictionary<Entity, List<ParsedLogEntry>> Logs = new Dictionary<Entity, List<ParsedLogEntry>>();

        public Dictionary<Entity,List<ParsedLogEntry>> OutgoingDamageLogs = new Dictionary<Entity, List<ParsedLogEntry>>();
        public Dictionary<Entity, List<ParsedLogEntry>> IncomingDamageLogs = new Dictionary<Entity, List<ParsedLogEntry>>();
        public Dictionary<Entity, List<ParsedLogEntry>> IncomingSheildedLogs = new Dictionary<Entity, List<ParsedLogEntry>>();
        public Dictionary<Entity, List<ParsedLogEntry>> OutgoingHealingLogs = new Dictionary<Entity, List<ParsedLogEntry>>();
        public Dictionary<Entity, List<ParsedLogEntry>> IncomingHealingLogs = new Dictionary<Entity, List<ParsedLogEntry>>();
        public Dictionary<Entity, List<ParsedLogEntry>> SheildingProvidedLogs = new Dictionary<Entity, List<ParsedLogEntry>>();

        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingDamageByTarget(Entity source)
        {
            return GetByTarget(OutgoingDamageLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingDamageBySource(Entity source)
        {
            return GetBySource(IncomingDamageLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingDamageByAbility(Entity source)
        {
            return GetByAbility(OutgoingDamageLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingDamageByAbility(Entity source)
        {
            return GetByAbility(IncomingDamageLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingHealingBySource(Entity source)
        {
            return GetBySource(IncomingHealingLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingHealingByAbility(Entity source)
        {
            return GetByAbility(IncomingHealingLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingHealingByTarget(Entity source)
        {
            return GetByTarget(OutgoingHealingLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingHealingByAbility(Entity source)
        {
            return GetByAbility(OutgoingHealingLogs[source]);
        }
        public Dictionary<string,List<ParsedLogEntry>> GetShieldingBySource(Entity source)
        {
            return GetBySource(IncomingSheildedLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetByTarget(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<string, List<ParsedLogEntry>>();
            var distinctTargets = logsToCheck.Select(l => l.Target.Name).Distinct();
            foreach (var target in distinctTargets)
            {
                returnDict[target] = logsToCheck.Where(l => l.Target.Name == target).ToList();
            }
            return returnDict;
        }
        public Dictionary<string, List<ParsedLogEntry>> GetBySource(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<string, List<ParsedLogEntry>>();
            var distinctSources = logsToCheck.Select(l => l.Source.Name).Distinct();
            foreach (var source in distinctSources)
            {
                returnDict[source] = logsToCheck.Where(l => l.Source.Name == source).ToList();
            }
            return returnDict;
        }
        public Dictionary<string, List<ParsedLogEntry>> GetByAbility(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<string, List<ParsedLogEntry>>();
            var distinctAbilities = logsToCheck.Select(l => l.Ability).Distinct();
            foreach (var ability in distinctAbilities)
            {
                returnDict[ability] = logsToCheck.Where(l => l.Ability == ability).ToList();
            }
            return returnDict;
        }

        public Dictionary<Entity,double> TotalAbilites = new Dictionary<Entity, double>();
        public Dictionary<Entity, double> TotalThreat = new Dictionary<Entity, double>();
        public Dictionary<Entity, double> TotalDamage => TotalFluffDamage.ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value+TotalFocusDamage[kvp.Key]);
        public Dictionary<Entity,double> TotalFluffDamage = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalFocusDamage = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalCompanionDamage = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalHealing = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalCompanionHealing = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalEffectiveHealing = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalEffectiveCompanionHealing = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalSheilding = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalProvidedSheilding = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalDamageTaken = new Dictionary<Entity, double>();
        public Dictionary<Entity, double> CurrentHealthDeficit => TotalFluffDamage.ToDictionary(kvp=>kvp.Key,kvp=>Math.Max(0, TotalEffectiveDamageTaken[kvp.Key]-TotalEffectiveHealingReceived[kvp.Key]));
        public Dictionary<Entity,double> TotalEffectiveDamageTaken = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> TotalHealingReceived = new Dictionary<Entity, double>();
        public Dictionary<Entity, double> TotalEffectiveHealingReceived = new Dictionary<Entity, double>();

        public Dictionary<Entity,double> TPS => DurationSeconds == 0 ? TotalThreat.ToDictionary(kvp => kvp.Key,kvp=>0d) : TotalThreat.ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> DPS => DurationSeconds == 0 ? TotalDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> RegDPS => DurationSeconds == 0 ? TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> FocusDPS => DurationSeconds == 0 ? TotalFocusDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalFocusDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> CompDPS => DurationSeconds == 0 ? TotalCompanionDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalCompanionDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> APM => DurationSeconds == 0 ? TotalAbilites.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalAbilites.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds/60);
        public Dictionary<Entity,double> HPS => DurationSeconds == 0 ? TotalHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> EHPS => DurationSeconds == 0 ? TotalEffectiveHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> CompEHPS => DurationSeconds == 0 ? TotalEffectiveCompanionHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveCompanionHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> SPS => DurationSeconds == 0 ? TotalSheilding.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalSheilding.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> PSPS => DurationSeconds == 0 ? TotalProvidedSheilding.ToDictionary(kvp => kvp.Key,kvp=>0d) : TotalProvidedSheilding.ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> DTPS => DurationSeconds == 0 ? TotalDamageTaken.ToDictionary(kvp => kvp.Key,kvp=>0d) : TotalDamageTaken.ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> EDTPS => DurationSeconds == 0 ?TotalEffectiveDamageTaken.ToDictionary(kvp => kvp.Key,kvp=>0d) : TotalEffectiveDamageTaken.ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> HTPS => DurationSeconds == 0 ? TotalHealingReceived.ToDictionary(kvp => kvp.Key,kvp=>0d) : TotalHealingReceived.ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value / DurationSeconds);
        public Dictionary<Entity,double> EHTPS => DurationSeconds == 0 ? TotalEffectiveHealingReceived.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveHealingReceived.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);

        public Dictionary<Entity,double> MaxDamage = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> MaxIncomingDamage = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> MaxEffectiveIncomingDamage = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> MaxHeal = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> MaxEffectiveHeal = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> MaxIncomingHeal = new Dictionary<Entity, double>();
        public Dictionary<Entity,double> MaxIncomingEffectiveHeal = new Dictionary<Entity, double>();
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
