using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public class Combat
    {
        public DateTime StartTime;
        public DateTime EndTime;
        public double DurationMS => (EndTime - StartTime).TotalMilliseconds;
        public double DurationSeconds => DurationMS / 1000f;
        public List<string> Targets;
        public List<ParsedLogEntry> Logs;

        public double TotalAbilites;
        public double TotalDamage;
        public double TotalHealing;
        public double TotalEffectiveHealing;
        public double TotalSheilding;
        public double TotalDamageTaken;
        public double TotalHealingReceived;
        public double TotalEffectiveHealingReceived;

        public double DPS => TotalDamage / DurationSeconds;
        public double APM => TotalAbilites / (DurationSeconds / 60);
        public double HPS => TotalHealing / DurationSeconds;
        public double EHPS => TotalEffectiveHealing / DurationSeconds;
        public double SPS => TotalSheilding / DurationSeconds;

        public double DTPS => TotalDamageTaken / DurationSeconds;
        public double HTPS => TotalHealingReceived / DurationSeconds;
        public double EHTPS => TotalEffectiveHealingReceived / DurationSeconds;

        public double MaxDamage;
        public double MaxIncomingDamage;
        public double MaxHeal;
        public double MaxEffectiveHeal;
        public double MaxIncomingHeal;
        public double MaxIncomingEffectiveHeal;

    }
    public class LogContents
    {
        public string Error;
        public List<Combat> Combats = new List<Combat>();
        public string SourceLog;
        public string Character;
    }
    public static class CombatIdentifier
    {
        public static LogContents GetMostRecentLogsCombat()
        {
            var log = CombatLogLoader.LoadMostRecentLog();
            var parsedLogs = CombatLogParser.ParseAllLines(log);
            if (parsedLogs.Count == 0)
                return new LogContents() {SourceLog = log.Name, Error = "No valid logs in log file "+log.Name };
            return GetActiveCombatLogs(parsedLogs);
        }
        public static LogContents GetActiveCombatLogs(List<ParsedLogEntry> allLogs)
        {
            var output = new LogContents() { SourceLog = allLogs[0].LogName, Character = GetCharacter(allLogs)};
            var listOfCombatStartEvents = allLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "EnterCombat").ToList();
            var listOfCombatExitEvents = allLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "ExitCombat").ToList();

            if (listOfCombatStartEvents.Count == 0)
                return output;

            var listOfValidCombatExits = new List<ParsedLogEntry>();

            var mostRecentCombatStart = listOfCombatStartEvents[0].TimeStamp;
            foreach (var combatExit in listOfCombatExitEvents)
            {
                if (combatExit.TimeStamp > mostRecentCombatStart)
                {
                    if (listOfValidCombatExits.Count > 0 && combatExit.TimeStamp > listOfValidCombatExits.Last().TimeStamp)
                        continue;
                    listOfValidCombatExits.Add(combatExit);
                    if (listOfValidCombatExits.Count == listOfCombatExitEvents.Count)
                        break;
                    mostRecentCombatStart = listOfCombatStartEvents[listOfValidCombatExits.Count].TimeStamp;
                }
            }


            for (var combats = 0; combats < listOfValidCombatExits.Count(); combats++)
            {
                var startEvent = listOfCombatStartEvents[combats];
                var stopEvent = listOfValidCombatExits[combats];

                var startIndex = allLogs.IndexOf(allLogs.First(l => l.TimeStamp == startEvent.TimeStamp));
                var stopIndex = allLogs.IndexOf(allLogs.First(l => l.TimeStamp == stopEvent.TimeStamp));
                var logsInCombat = allLogs.GetRange(startIndex, (stopIndex - startIndex)+10);
                var newCombat = new Combat()
                {
                    StartTime = startEvent.TimeStamp,
                    EndTime = stopEvent.TimeStamp,
                    Targets = GetTargets(logsInCombat),
                    Logs = logsInCombat
                };
                CombatMetaDataParse.PopulateMetaData(ref newCombat);
                output.Combats.Add(newCombat);
            }
            return output;
        }
        public static Combat ParseOngoingCombat(List<ParsedLogEntry> ongoingLogs)
        {
            var newCombat = new Combat()
            {
                StartTime = ongoingLogs.First().TimeStamp,
                EndTime = ongoingLogs.Last().TimeStamp,
                Targets = GetTargets(ongoingLogs),
                Logs = ongoingLogs
            };
            CombatMetaDataParse.PopulateMetaData(ref newCombat);
            return newCombat;
        }
        private static string GetCharacter(List<ParsedLogEntry> logs)
        {
            return logs.First(f => f.Source.IsCharacter).Source.Name;
        }
        private static List<string> GetTargets(List<ParsedLogEntry> logs)
        {
            return logs.Select(l=>l.Target).Where(t=>!t.IsCharacter).Select(npc=>npc.Name).Distinct().ToList();
        }
    }
}
