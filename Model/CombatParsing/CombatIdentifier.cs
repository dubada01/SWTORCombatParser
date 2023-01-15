//using MoreLinq;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatIdentifier
    {
        public static event Action<Combat> NewCombatAvailable = delegate { };
        public static event Action<(string, string, string),DateTime> NewBossCombatDetected = delegate { };
        public static event Action NewCombatStarted = delegate { };
        private static object leaderboardLock = new object();

        private static bool _leaderboardsActive;
        public static Combat CurrentCombat { get; set; }
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
            FireEvent(combat);
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
            FireEvent(combat);
        }
        public static void FireEvent(Combat combat)
        {
            CurrentCombat = combat;
            NewCombatAvailable(combat);
        }
        public static void NotifyNewCombatStarted()
        {
            _leaderboardsActive = false;
            NewCombatStarted();
        }

        public static Combat GenerateNewCombatFromLogs(List<ParsedLogEntry> ongoingLogs, bool isRealtime = false, bool quietOverlays = false, bool combatEndUpdate = false)
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
            if (encounter !=  null && encounter.BossInfos != null)
            {
                newCombat.ParentEncounter = encounter;
                newCombat.EncounterBossDifficultyParts = GetCurrentBossInfo(ongoingLogs,encounter);
                newCombat.RequiredDeadTargetsForKill = GetTargetsRequiredForKill(ongoingLogs, encounter);
                if(!string.IsNullOrEmpty(newCombat.EncounterBossDifficultyParts.Item1) && isRealtime && !combatEndUpdate)
                    NewBossCombatDetected(newCombat.EncounterBossDifficultyParts,newCombat.StartTime);
            }
            if (newCombat.IsCombatWithBoss)
            {
                var parts = newCombat.EncounterBossDifficultyParts;
                if(isRealtime)
                    EncounterTimerTrigger.FireEncounterDetected(newCombat.ParentEncounter.Name, parts.Item1, newCombat.ParentEncounter.Difficutly);
            }
            if (newCombat.Targets.Any(t => t.LogId == 2857785339412480))
            {
                newCombat.ParentEncounter = new EncounterInfo() { Name = "Parsing", LogName = "Parsing", Difficutly = "Parsing", NumberOfPlayer = "1",EncounterType = EncounterType.Parsing, BossIds = new Dictionary<string, Dictionary<string, List<long>>>() {{"Training Dummy",new Dictionary<string, List<long>>(){{"Parsing 1",new List<long>{2857785339412480}}}} } };
                newCombat.EncounterBossDifficultyParts = GetCurrentBossInfo(ongoingLogs, encounter);
                newCombat.RequiredDeadTargetsForKill = new List<string> { "2857785339412480" };
            }
            CombatMetaDataParse.PopulateMetaData(newCombat);
            var absorbLogs = newCombat.IncomingDamageMitigatedLogs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Where(l=>l.Value.Modifier.ValueType == DamageType.absorbed).ToList());
            AddSheildingToLogs.AddShieldLogsByTarget(absorbLogs, newCombat);
            AddTankCooldown.AddDamageSavedDuringCooldown(newCombat);
            if(!quietOverlays)
                FireEvent(newCombat);
            return newCombat;
        }

        private static List<Entity> GetTargets(List<ParsedLogEntry> logs)
        {
            var combatStart = logs.OrderBy(t => t.TimeStamp).First().TimeStamp;
            var validLogs = logs.Where(l => l.Effect.EffectType != EffectType.TargetChanged && l.Effect.EffectId == _7_0LogParsing._damageEffectId);
            var parsedLogEntries = validLogs.ToList();
            var targets = parsedLogEntries.Select(l => l.Target).Where(
                t => (!t.IsCharacter || CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(t,combatStart)) 
                     && !t.IsCompanion 
                     && t.Name != null).ToList();
            var sources = parsedLogEntries.Select(l => l.Source)
                .Where(s => (!s.IsCharacter|| CombatLogStateBuilder.CurrentState.IsPvpOpponentAtTime(s,combatStart))
                    && !s.IsCompanion 
                    && s.Name != null);
            targets.AddRange(sources);

            return targets.DistinctBy(t=>t.Name).ToList();
        }
        private static EncounterInfo GetEncounterInfo(DateTime combatStartTime)
        {
            return CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(combatStartTime);
        }
        private static (string, string, string) GetCurrentBossInfo(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null)
                return ("", "", "");

            var validLogs = logs.Where(l => l.Effect.EffectType != EffectType.TargetChanged && !string.IsNullOrEmpty(l.Target.Name) && l.Effect.EffectId == _7_0LogParsing._damageEffectId).ToList();
            if (currentEncounter.Name.Contains("Open World"))
            {
                if (validLogs.Select(l => l.Target).DistinctBy(t => t.Id).Any(t => t.LogId == 2857785339412480))
                {
                    var dummyTarget = validLogs.Select(l => l.TargetInfo).First(t => t.Entity.LogId == 2857785339412480);
                    var dummyMaxHP = dummyTarget.MaxHP;
                    currentEncounter.Difficutly = dummyMaxHP.ToString();
                    return (dummyTarget.Entity.Name, dummyMaxHP + "HP", "");
                }
                else
                {
                    return ("", "", "");
                }
            }
            var bossesDetected = GetCurrentBossNames(validLogs, currentEncounter);
            if (!bossesDetected.Any())
                return ("", "", "");
            var boss = currentEncounter.BossInfos.FirstOrDefault(b => bossesDetected.All(t => b.TargetIds.Contains(t)));
            if (boss!=null)
            {
                return (boss.EncounterName, currentEncounter.NumberOfPlayer.Replace("Player", "").Trim(), currentEncounter.Difficutly);
            }

            return ("", "", "");
        }
        private static List<string> GetCurrentBossNames(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return new List<string>();
            var bossNamesFound = new List<string>();
            foreach (var log in logs)
            {
                if (currentEncounter.BossInfos.SelectMany(b => b.TargetIds).Contains(log.Source.LogId.ToString()))
                {
                    log.Source.IsBoss = true;
                    bossNamesFound.Add(log.Source.LogId.ToString());
                }
                if(currentEncounter.BossInfos.SelectMany(b => b.TargetIds).Contains(log.Target.LogId.ToString()))
                {
                    log.Target.IsBoss = true;
                    bossNamesFound.Add(log.Target.LogId.ToString());
                }
            }
            return bossNamesFound.Distinct().ToList();
        }
        private static List<string> GetTargetsRequiredForKill(List<ParsedLogEntry> logs, EncounterInfo currentEncounter)
        {
            if (currentEncounter == null || currentEncounter.Name.Contains("Open World"))
                return new List<string>();
            var validLogs = logs.Where(l => l.Effect.EffectType != EffectType.TargetChanged && l.Effect.EffectId == _7_0LogParsing._damageEffectId).ToList();
            var bossesDetected = GetCurrentBossNames(validLogs, currentEncounter);
            if (!bossesDetected.Any())
                return new List<string>();
            var boss = currentEncounter.BossInfos.FirstOrDefault(b => bossesDetected.All(t => b.TargetIds.Contains(t)));
            if (boss != null)
            {
                return boss.TargetsRequiredForKill;
            }
            return new List<string>();
        }
    }
}
