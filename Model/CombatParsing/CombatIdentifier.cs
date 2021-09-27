using SWTORCombatParser.DataStructures;
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
        public static event Action NewCombatStarted = delegate { };
        public static void UpdateOverlays(Combat combat)
        {
            NewCombatAvailable(combat);
        }
        public static void NotifyNewCombatStarted()
        {
            NewCombatStarted();
        }
        public static void ResetLeaderboardOverlay()
        {

        }
        public static Combat GenerateNewCombatFromLogs(List<ParsedLogEntry> ongoingLogs)
        {
            var encounter = GetEncounterInfo(ongoingLogs);
            var currentPariticpants = ongoingLogs.Where(l => l.Source.IsCharacter || l.Source.IsCompanion).Select(p => p.Source).Distinct().ToList();
            currentPariticpants.AddRange(ongoingLogs.Where(l => l.Target.IsCharacter || l.Target.IsCompanion).Select(p => p.Target).Distinct().ToList());
            var participants = currentPariticpants.Distinct().ToList();
            var newCombat = new Combat()
            {
                CharacterParticipants = participants,
                StartTime = ongoingLogs.OrderBy(t => t.TimeStamp).First().TimeStamp,
                EndTime = ongoingLogs.OrderBy(t => t.TimeStamp).Last().TimeStamp,
                Targets = GetTargets(ongoingLogs),
                ParentEncounter = encounter,
                EncounterBossInfo = GetCurrentBossInfo(ongoingLogs, encounter),
                Logs = SplitLogsByParticipant(ongoingLogs, participants)
            };
            CombatMetaDataParse.PopulateMetaData(ref newCombat);
            var sheildLogs = newCombat.IncomingDamageMitigatedLogs;
            AddSheildingToLogs.AddSheildLogs(sheildLogs, newCombat);
            return newCombat;
        }

        private static Dictionary<Entity, List<ParsedLogEntry>> SplitLogsByParticipant(List<ParsedLogEntry> ongoingLogs, List<Entity> currentPariticpants)
        {
            var logsByParticipant = new Dictionary<Entity, List<ParsedLogEntry>>();
            foreach(var particpant in currentPariticpants)
            {
                logsByParticipant[particpant] = ongoingLogs.Where(l => l.Source == particpant || l.Target == particpant).ToList();
            }
            return logsByParticipant;
        }

        private static List<Entity> GetTargets(List<ParsedLogEntry> logs)
        {
            var targets = logs.Select(l => l.Target).Where(t => !t.IsCharacter && !t.IsCompanion).ToList();
            targets.AddRange(logs.Select(l => l.Source).Where(t => !t.IsCharacter && !t.IsCompanion));
            return targets.Distinct().ToList();
        }
        private static EncounterInfo GetEncounterInfo(List<ParsedLogEntry> logs)
        {
            EncounterInfo raidOfInterest = null;

            foreach (var log in logs)
            {
                var knownEncounters = RaidNameLoader.SupportedEncounters;
                if(log.Target.Name == "Operations Training Dummy")
                {
                    raidOfInterest = new EncounterInfo() { BossInfos = new List<BossInfo>() };
                    raidOfInterest.Name = "Parsing";
                    raidOfInterest.BossInfos.Add(new BossInfo { 
                        EncounterName = "Training Dummy",
                        TargetNames = new List<string> { "Operations Training Dummy" }
                    });
                    raidOfInterest.Difficutly = "Parsing";
                    raidOfInterest.NumberOfPlayer = "";
                    return raidOfInterest;
                }
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
