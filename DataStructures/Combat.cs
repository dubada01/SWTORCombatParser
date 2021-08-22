using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public class Combat
    {
        public string CharacterName;
        public DateTime StartTime;
        public DateTime EndTime;
        public double DurationMS => (EndTime - StartTime).TotalMilliseconds;
        public double DurationSeconds => DurationMS / 1000f;
        public List<string> Targets;
        public string RaidBossInfo;
        public List<ParsedLogEntry> Logs;

        public List<ParsedLogEntry> OutgoingDamageLogs;
        public List<ParsedLogEntry> IncomingDamageLogs;
        public List<ParsedLogEntry> IncomingSheildedLogs;
        public List<ParsedLogEntry> OutgoingHealingLogs;
        public List<ParsedLogEntry> IncomingHealingLogs;

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

        public double TPS => TotalThreat / DurationSeconds;
        public double DPS => TotalDamage / DurationSeconds == double.NaN?0: TotalDamage / DurationSeconds;
        public double APM => TotalAbilites / (DurationSeconds / 60);
        public double HPS => TotalHealing / DurationSeconds;
        public double EHPS => TotalEffectiveHealing / DurationSeconds;
        public double SPS => TotalSheilding / DurationSeconds;
        public double PSPS => TotalProvidedSheilding / DurationSeconds;
        public double DTPS => TotalDamageTaken / DurationSeconds;
        public double EDTPS => TotalEffectiveDamageTaken / DurationSeconds;
        public double HTPS => TotalHealingReceived / DurationSeconds;
        public double EHTPS => TotalEffectiveHealingReceived / DurationSeconds;

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
