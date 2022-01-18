using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public static class CombatIdentifier
    {
        public static event Action<Combat> NewCombatAvailable = delegate { };
        public static event Action NewCombatStarted = delegate { };
        private static object leaderboardLock = new object();
       
        public static void UpdateOverlays(Combat combat)
        {
            if (combat.IsEncounterBoss)
            {
                Task.Run(() =>
                {
                    lock (leaderboardLock)
                    {
                        Leaderboards.Reset();
                        Leaderboards.StartGetPlayerLeaderboardStandings(combat);
                        Leaderboards.StartGetTopLeaderboardEntries(combat);
                    }
                });
            }
            NewCombatAvailable(combat);
        }
        public static void NotifyNewCombatStarted()
        {
            NewCombatStarted();
        }
        public static void ResetLeaderboardOverlay()
        {

        }
        public static List<Combat> GetAllBossCombatsFromLog(List<ParsedLogEntry> allLogsFromfile)
        {
            List<Combat> combats = new List<Combat>();
            List<ParsedLogEntry> currentCombatLogs = new List<ParsedLogEntry>();

            foreach(var line in allLogsFromfile)
            {
                var combatState = CombatDetector.CheckForCombatState(line);
                if(combatState == CombatState.EnteredCombat)
                {
                    currentCombatLogs = new List<ParsedLogEntry>();
                }
                if(combatState == CombatState.ExitedCombat)
                {
                    if (currentCombatLogs.Count == 0)
                        continue;
                    var combatCreated = GenerateNewCombatFromLogs(currentCombatLogs);
                    if (!string.IsNullOrEmpty(combatCreated.EncounterBossInfo))
                        combats.Add(combatCreated);
                }
                if (combatState == CombatState.InCombat)
                    currentCombatLogs.Add(line);
            }
            return combats;
        }
        public static Combat GenerateNewCombatFromLogs(List<ParsedLogEntry> ongoingLogs)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var encounter = GetEncounterInfo(ongoingLogs.OrderBy(t => t.TimeStamp).First().TimeStamp);
            var currentPariticpants = ongoingLogs.Where(l => l.Source.IsCharacter || l.Source.IsCompanion).Select(p => p.Source).Distinct().ToList();
            currentPariticpants.AddRange(ongoingLogs.Where(l => l.Target.IsCharacter || l.Target.IsCompanion).Select(p => p.Target).Distinct().ToList());
            var participants = currentPariticpants.GroupBy(p => p.Id).Select(x => x.FirstOrDefault()).ToList();
            var participantInfos = ongoingLogs.Select(p => p.SourceInfo).Distinct().ToList();
            var classes = participantInfos.GroupBy(p => p.Entity.Id).Select(x => x.FirstOrDefault()).ToDictionary(k => k.Entity, k => state.GetCharacterClassAtTime(k.Entity,ongoingLogs.First().TimeStamp));
            var newCombat = new Combat()
            {
                CharacterParticipants = participants,
                CharacterClases = classes,
                StartTime = ongoingLogs.OrderBy(t => t.TimeStamp).First().TimeStamp,
                EndTime = ongoingLogs.OrderBy(t => t.TimeStamp).Last().TimeStamp,
                Targets = GetTargets(ongoingLogs),
                AllLogs = ongoingLogs
            };
            if (encounter !=  null && encounter.BossInfos != null)
            {
                newCombat.ParentEncounter = encounter;
                newCombat.EncounterBossInfo = GetCurrentBossInfo(ongoingLogs,encounter);
                newCombat.RequiredDeadTargetsForKill = GetCurrentBossNames(ongoingLogs, encounter);
            }
            CombatMetaDataParse.PopulateMetaData(newCombat);
            var absorbLogs = newCombat.IncomingDamageMitigatedLogs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Where(l=>l.Value.Modifier.ValueType == DamageType.absorbed).ToList());
            AddSheildingToLogs.AddSheildLogs(absorbLogs, newCombat);
            return newCombat;
        }

        private static List<Entity> GetTargets(List<ParsedLogEntry> logs)
        {
            var validLogs = logs.Where(l => l.Effect.EffectName != "TargetSet");
            var targets = validLogs.Select(l => l.Target).Where(t => !t.IsCharacter && !t.IsCompanion && t.Name != null).ToList();
            targets.AddRange(validLogs.Select(l => l.Source).Where(t => !t.IsCharacter && !t.IsCompanion && t.Name != null));
            return targets.DistinctBy(t=>t.Name).ToList();
        }
        private static EncounterInfo GetEncounterInfo(DateTime combatStartTime)
        {
            return CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(combatStartTime);
        }
        private static string GetCurrentBossInfo(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return "";
            var validLogs = logs.Where(l => l.Effect.EffectName != "TargetSet");
            foreach (var log in validLogs)
            {
                if (currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Source.Name) || currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Target.Name))
                {
                    var boss = currentEncounter.BossInfos.First(b => b.TargetNames.Contains(log.Source.Name) || b.TargetNames.Contains(log.Target.Name));
                    var bossTargetString = boss.EncounterName + " {" + currentEncounter.NumberOfPlayer.Replace("Player", "") + currentEncounter.Difficutly + "}";
                    return bossTargetString;
                }
            }
            return "";
        }
        private static List<string> GetCurrentBossNames(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return new List<string>();
            var validLogs = logs.Where(l => l.Effect.EffectName != "TargetSet");
            foreach (var log in validLogs)
            {
                if (currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Source.Name) || currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Target.Name))
                {
                    var boss = currentEncounter.BossInfos.First(b => b.TargetNames.Contains(log.Source.Name) || b.TargetNames.Contains(log.Target.Name));
                    return boss.TargetNames;
                }
            }
            return new List<string>();
        }
    }
}
