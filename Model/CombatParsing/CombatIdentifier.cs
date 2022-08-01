//using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
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

        private static bool _leaderboardsActive;

        public static void FinalizeOverlays(Combat combat)
        {
            _leaderboardsActive = false;
            UpdateOverlays(combat);
        }
        public static void UpdateOverlays(Combat combat)
        {
            if (combat.IsCombatWithBoss)
            {
                Task.Run(() => {
                    if (!_leaderboardsActive)
                    {
                        Leaderboards.Reset();
                        Leaderboards.StartGetTopLeaderboardEntries(combat);
                        _leaderboardsActive = true;
                    }

                    Leaderboards.StartGetPlayerLeaderboardStandings(combat);
                });
             }
            NewCombatAvailable(combat);
        }
        public static void FinalizeOverlay(Combat combat)
        {
            if (combat.IsCombatWithBoss)
            {
                Task.Run(() =>
                {
                    Leaderboards.Reset();
                    Leaderboards.StartGetTopLeaderboardEntries(combat);
                    Leaderboards.StartGetPlayerLeaderboardStandings(combat);
                });
            }
            NewCombatAvailable(combat);
        }
        public static void NotifyNewCombatStarted()
        {
            _leaderboardsActive = false;
            NewCombatStarted();
        }

        public static Combat GenerateNewCombatFromLogs(List<ParsedLogEntry> ongoingLogs)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var encounter = GetEncounterInfo(ongoingLogs.OrderBy(t => t.TimeStamp).First().TimeStamp);
            var currentPariticpants = ongoingLogs.Where(l => l.Source.IsCharacter || l.Source.IsCompanion).Where(l=>l.Effect.EffectType != EffectType.TargetChanged).Select(p => p.Source).Distinct().ToList();
            currentPariticpants.AddRange(ongoingLogs.Where(l => l.Target.IsCharacter || l.Target.IsCompanion).Where(l => l.Effect.EffectType != EffectType.TargetChanged).Select(p => p.Target).Distinct().ToList());
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
            if(newCombat.Targets.Any(t=>t.Name.Contains("Training Dummy")))
            {
                newCombat.ParentEncounter = new EncounterInfo() { Name = "Parsing", LogName = "Parsing", Difficutly = "Unknown", NumberOfPlayer = "1", BossNames = new List<string> { "Warzone Training Dummy", "Operations Training Dummy" } };
                newCombat.EncounterBossDifficultyParts = GetCurrentBossInfo(ongoingLogs, encounter);
                newCombat.RequiredDeadTargetsForKill = GetCurrentBossNames(ongoingLogs, newCombat.ParentEncounter);
            }
            if (encounter !=  null && encounter.BossInfos != null)
            {
                newCombat.ParentEncounter = encounter;
                newCombat.EncounterBossDifficultyParts = GetCurrentBossInfo(ongoingLogs,encounter);
                newCombat.RequiredDeadTargetsForKill = GetCurrentBossNames(ongoingLogs, encounter);
            }
            if (newCombat.IsCombatWithBoss)
            {
                var parts = newCombat.EncounterBossDifficultyParts;
                EncounterTimerTrigger.FireEncounterDetected(newCombat.ParentEncounter.Name, parts.Item1, newCombat.ParentEncounter.Difficutly);
            }
            CombatMetaDataParse.PopulateMetaData(newCombat);
            var absorbLogs = newCombat.IncomingDamageMitigatedLogs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Where(l=>l.Value.Modifier.ValueType == DamageType.absorbed).ToList());
            AddSheildingToLogs.AddShieldLogsByTarget(absorbLogs, newCombat);
            return newCombat;
        }

        private static List<Entity> GetTargets(List<ParsedLogEntry> logs)
        {
            var validLogs = logs.Where(l => l.Effect.EffectType != EffectType.TargetChanged && l.Effect.EffectName == "Damage");
            var targets = validLogs.Select(l => l.Target).Where(t => !t.IsCharacter && !t.IsCompanion && t.Name != null).ToList();
            targets.AddRange(validLogs.Select(l => l.Source).Where(t => !t.IsCharacter && !t.IsCompanion && t.Name != null));
            return targets.DistinctBy(t=>t.Name).ToList();
        }
        private static EncounterInfo GetEncounterInfo(DateTime combatStartTime)
        {
            return CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(combatStartTime);
        }
        private static (string,string,string) GetCurrentBossInfo(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null)
                return ("","","");
            var validLogs = logs.Where(l => l.Effect.EffectType != EffectType.TargetChanged && !string.IsNullOrEmpty(l.Target.Name) && l.Effect.EffectName == "Damage");
            if (currentEncounter.Name.Contains("Open World"))
            {
                if (validLogs.Select(l => l.Target).DistinctBy(t => t.Id).Any(t => t.Name.Contains("Training Dummy")))
                {
                    var dummyTarget = validLogs.Select(l => l.TargetInfo).First(t => t.Entity.Name.Contains("Training Dummy"));
                    var dummyMaxHP = dummyTarget.MaxHP;
                    return ("Parsing Dummy", dummyMaxHP + "HP","");
                }
                else
                {
                    return ("", "", "");
                }
            }

            foreach (var log in validLogs)
            {
                if (currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Source.Name) || currentEncounter.BossInfos.SelectMany(b => b.TargetNames).Contains(log.Target.Name))
                {
                    var boss = currentEncounter.BossInfos.First(b => b.TargetNames.Contains(log.Source.Name) || b.TargetNames.Contains(log.Target.Name));
                    return (boss.EncounterName, currentEncounter.NumberOfPlayer.Replace("Player", "").Trim(), currentEncounter.Difficutly);
                }
            }
            return ("", "", "");
        }
        private static List<string> GetCurrentBossNames(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return new List<string>();
            var validLogs = logs.Where(l => l.Effect.EffectType != EffectType.TargetChanged && l.Effect.EffectName == "Damage");
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
