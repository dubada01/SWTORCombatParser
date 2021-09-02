using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Raiding;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public static class CombatIdentifier
    {
        public static event Action<Combat> NewCombatAvailable = delegate { };
        public static void UpdateOngoingCombat(List<ParsedLogEntry> newLogs, Combat combatToUpdate)
        {
            var orderdLogs = newLogs.OrderBy(t => t.TimeStamp);
            combatToUpdate.Logs.AddRange(orderdLogs);
            //combatToUpdate.CharacterName = combatToUpdate.Logs.First(l => l.Source == combatToUpdate.Owner).Source.Name;
            combatToUpdate.StartTime = combatToUpdate.Logs.First().TimeStamp;
            combatToUpdate.EndTime = combatToUpdate.Logs.Last().TimeStamp;
            combatToUpdate.Targets.AddRange(GetTargets(newLogs));
            combatToUpdate.Targets = GetTargets(combatToUpdate.Logs);
            combatToUpdate.ParentEncounter = GetEncounterInfo(combatToUpdate.Logs);
            combatToUpdate.EncounterBossInfo = GetCurrentBossInfo(combatToUpdate.Logs, combatToUpdate.ParentEncounter);
            CombatMetaDataParse.PopulateMetaData(ref combatToUpdate);
            NewCombatAvailable(combatToUpdate);
        }
        public static Combat GenerateNewCombatFromLogs(List<ParsedLogEntry> ongoingLogs)
        {
            if (!ongoingLogs.Any(l => (l.Source.IsPlayer && l.Target.IsPlayer)))
                return new Combat();
            var encounter = GetEncounterInfo(ongoingLogs);
            var newCombat = new Combat()
            {
                CharacterName = ongoingLogs.First(l => (l.Source.IsPlayer && l.Target.IsPlayer)).Source.Name,
                StartTime = ongoingLogs.OrderBy(t => t.TimeStamp).First().TimeStamp,
                EndTime = ongoingLogs.OrderBy(t => t.TimeStamp).Last().TimeStamp,
                Targets = GetTargets(ongoingLogs),
                ParentEncounter = encounter,
                EncounterBossInfo = GetCurrentBossInfo(ongoingLogs, encounter),
                Logs = ongoingLogs,
            };
            CombatMetaDataParse.PopulateMetaData(ref newCombat);
            var sheildLogs = newCombat.IncomingSheildedLogs;
            AddSheildingToLogs.AddSheildLogs(CombatLogStateBuilder.GetLocalState(), sheildLogs, newCombat);
            
            NewCombatAvailable(newCombat);
            return newCombat;
        }
        private static List<string> GetTargets(List<ParsedLogEntry> logs)
        {
            return logs.Select(l=>l.Target).Where(t=>!t.IsCharacter && !t.IsCompanion).Select(npc=>npc.Name).Distinct().ToList();
        }
        private static EncounterInfo GetEncounterInfo(List<ParsedLogEntry> logs)
        {
            EncounterInfo raidOfInterest = null;

            foreach (var log in logs)
            {
                var knownEncounters = RaidNameLoader.SupportedEncounters;
                if (!string.IsNullOrEmpty(log.Value.StrValue) && knownEncounters.Select(r => r.LogName).Any(ln => log.Value.StrValue.Contains(ln)) && raidOfInterest == null)
                {
                    raidOfInterest = knownEncounters.First(r => log.Value.StrValue.Contains(r.LogName));
                    raidOfInterest.Difficutly = RaidNameLoader.SupportedRaidDifficulties.FirstOrDefault(f => log.Value.StrValue.Contains(f));
                    if (string.IsNullOrEmpty(raidOfInterest.Difficutly))
                    {
                        raidOfInterest.Difficutly = "Test";
                        //return null;
                    }
                    raidOfInterest.NumberOfPlayer = RaidNameLoader.SupportedNumberOfPlayers.FirstOrDefault(f => log.Value.StrValue.Contains(f));
                    if (string.IsNullOrEmpty(raidOfInterest.NumberOfPlayer))
                    {
                        raidOfInterest.NumberOfPlayer = "";
                        //return null;
                    }
                    Trace.WriteLine("Detected: " + raidOfInterest.Name);
                    return raidOfInterest;
                }
            }
            var openWorldLocation = "";
            var enterCombatLog = logs.FirstOrDefault(l => l.Effect.EffectName == "EnterCombat");
            if (enterCombatLog != null)
            {
                openWorldLocation = ": " + enterCombatLog.Value.StrValue;
            }
            return new EncounterInfo { Name="Open World"+openWorldLocation, LogName = "Open World"};
        }
        private static string GetCurrentBossInfo(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return "";
            foreach(var log in logs)
            {
                if (currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Source.Name) || currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Target.Name))
                {
                    var boss = currentEncounter.BossInfos.First(b => b.TargetNames.Contains(log.Source.Name) || b.TargetNames.Contains(log.Target.Name));
                    var bossTargetString = boss.EncounterName + " {" + currentEncounter.NumberOfPlayer.Replace("Player", "") + currentEncounter.Difficutly + "}";
                    Trace.WriteLine(bossTargetString);
                    return bossTargetString;
                }
            }
            return "";
        }
    }
}
