using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{

    public class LogContents
    {
        public string Error;
        public List<Combat> Combats = new List<Combat>();
        public string SourceLog;
        public string Character;
    }
    public static class CombatIdentifier
    {
        public static LogContents GetSpecificCombats(string logName)
        {
            var log = CombatLogLoader.LoadSpecificLog(logName);
            var parsedLogs = CombatLogParser.ParseAllLines(log);
            if (parsedLogs.Count == 0)
                return new LogContents() { SourceLog = log.Name, Error = "No valid logs in log file " + log.Name };
            return GetActiveCombatLogs(parsedLogs);
        }
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
            var listOfCombatExitEvents = allLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "ExitCombat" || l.Effect.EffectName == "Death").ToList();

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
            Trace.WriteLine("Length: " + ongoingLogs.Count);
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
            return logs.First(f => f.Source.IsPlayer).Source.Name;
        }
        private static List<string> GetTargets(List<ParsedLogEntry> logs)
        {
            return logs.Select(l=>l.Target).Where(t=>!t.IsCharacter).Select(npc=>npc.Name).Distinct().ToList();
        }
    }
}
