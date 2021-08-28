using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public class Combat
    {
        public Entity Owner => Logs.FirstOrDefault(l=>(l.Source == l.Target) && l.Source.IsPlayer || l.Source.IsCompanion)?.Source;
        public string CharacterName;
        public DateTime StartTime;
        public DateTime EndTime;
        public double DurationMS => (EndTime - StartTime).TotalMilliseconds;
        public double DurationSeconds => DurationMS / 1000f;
        public List<string> Targets = new List<string>();
        public string RaidBossInfo;
        public List<ParsedLogEntry> Logs = new List<ParsedLogEntry>();

        public List<ParsedLogEntry> OutgoingDamageLogs;
        public List<ParsedLogEntry> IncomingDamageLogs;
        public List<ParsedLogEntry> IncomingSheildedLogs;
        public List<ParsedLogEntry> OutgoingHealingLogs;
        public List<ParsedLogEntry> IncomingHealingLogs;
        public List<ParsedLogEntry> SheildingProvidedLogs = new List<ParsedLogEntry>();

        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingDamageByTarget()
        {
            return GetByTarget(OutgoingDamageLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingDamageBySource()
        {
            return GetBySource(IncomingDamageLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingDamageByAbility()
        {
            return GetByAbility(OutgoingDamageLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingDamageByAbility()
        {
            return GetByAbility(IncomingDamageLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingHealingBySource()
        {
            return GetBySource(IncomingHealingLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingHealingByAbility()
        {
            return GetByAbility(IncomingHealingLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingHealingByTarget()
        {
            return GetByTarget(OutgoingHealingLogs);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingHealingByAbility()
        {
            return GetByAbility(OutgoingHealingLogs);
        }
        public Dictionary<string,List<ParsedLogEntry>> GetShieldingBySource()
        {
            return GetBySource(IncomingSheildedLogs);
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

        public double TotalAbilites;
        public double TotalThreat;
        public double TotalDamage;
        public double TotalHealing;
        public double TotalEffectiveHealing;
        public double TotalSheilding;
        public double TotalProvidedSheilding;
        public double TotalDamageTaken;
        public double TotalEffectiveDamageTaken;
        public double TotalHealingReceived;
        public double TotalEffectiveHealingReceived;

        public double TPS => DurationSeconds == 0 ? 0: TotalThreat / DurationSeconds;
        public double DPS => DurationSeconds == 0 ? 0 : TotalDamage / DurationSeconds == double.NaN?0: TotalDamage / DurationSeconds;
        public double APM => DurationSeconds == 0 ? 0 : TotalAbilites / (DurationSeconds / 60);
        public double HPS => DurationSeconds == 0 ? 0 : TotalHealing / DurationSeconds;
        public double EHPS => DurationSeconds == 0 ? 0 : TotalEffectiveHealing / DurationSeconds;
        public double SPS => DurationSeconds == 0 ? 0 : TotalSheilding / DurationSeconds;
        public double PSPS => DurationSeconds == 0 ? 0 : TotalProvidedSheilding / DurationSeconds;
        public double DTPS => DurationSeconds == 0 ? 0 : TotalDamageTaken / DurationSeconds;
        public double EDTPS => DurationSeconds == 0 ? 0 : TotalEffectiveDamageTaken / DurationSeconds;
        public double HTPS => DurationSeconds == 0 ? 0 : TotalHealingReceived / DurationSeconds;
        public double EHTPS => DurationSeconds == 0 ? 0 : TotalEffectiveHealingReceived / DurationSeconds;

        public double MaxDamage;
        public double MaxIncomingDamage;
        public double MaxEffectiveIncomingDamage;
        public double MaxHeal;
        public double MaxEffectiveHeal;
        public double MaxIncomingHeal;
        public double MaxIncomingEffectiveHeal;
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
