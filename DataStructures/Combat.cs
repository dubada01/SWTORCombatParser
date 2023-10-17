using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SWTORCombatParser.DataStructures
{
    public class Combat
    {
        public Entity LocalPlayer => CharacterParticipants.FirstOrDefault(p => p.IsLocalPlayer);
        public List<Entity> CharacterParticipants = new List<Entity>();
        public Dictionary<Entity, SWTORClass> CharacterClases = new Dictionary<Entity, SWTORClass>();
        public List<Entity> Targets = new List<Entity>();
        public List<Entity> AllEntities => new List<Entity>().Concat(Targets).Concat(CharacterParticipants).ToList();
        public DateTime StartTime;
        public DateTime EndTime;
        public string LogFileName => AllLogs.Where(l => !string.IsNullOrEmpty(l.LogName)).First().LogName;
        public double DurationMS => (EndTime - StartTime).TotalMilliseconds;
        public int DurationSeconds => (int)Math.Round(DurationMS / 1000f);


        public EncounterInfo.EncounterInfo ParentEncounter;
        public BossInfo BossInfo;
        public string EncounterBossInfo => EncounterBossDifficultyParts == ("", "", "") ? "" : $"{EncounterBossDifficultyParts.Item1} {{{EncounterBossDifficultyParts.Item2} {EncounterBossDifficultyParts.Item3}}}";
        public string OldFlashpointBossInfo => EncounterBossDifficultyParts == ("", "", "") ? "" : $"{EncounterBossDifficultyParts.Item1} {{{EncounterBossDifficultyParts.Item3}}}";
        public (string, string, string) EncounterBossDifficultyParts = ("", "", "");

        public List<string> RequiredDeadTargetsForKill => BossInfo.TargetsRequiredForKill;
        public bool IsCombatWithBoss => !string.IsNullOrEmpty(EncounterBossInfo);
        public bool IsPvPCombat => Targets.Any(t => t.IsCharacter) && CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(StartTime).IsPvpEncounter;
        public bool WasBossKilled => RequiredDeadTargetsForKill.Count > 0 && RequiredDeadTargetsForKill.All(t => AllLogs.Where(t => t.Target.IsBoss).Any(l => (l.Target.LogId.ToString() == t && l.Effect.EffectId == _7_0LogParsing.DeathCombatId)));
        public List<ParsedLogEntry> AllLogs { get; set; } = new List<ParsedLogEntry>();
        public Dictionary<Entity, List<ParsedLogEntry>> LogsInvolvingEntity = new Dictionary<Entity, List<ParsedLogEntry>>();

        public List<ParsedLogEntry> GetLogsInvolvingEntity(Entity e)
        {
            try
            {
                return LogsInvolvingEntity[e];
            }
            catch (KeyNotFoundException ex)
            {
                return new List<ParsedLogEntry>();
            }
        }
        public bool WasPlayerKilled(Entity player)
        {
            return GetLogsInvolvingEntity(player).Any(l => l.Target == player && l.Effect.EffectId == _7_0LogParsing.DeathCombatId);
        }
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> OutgoingDamageLogs = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> IncomingDamageLogs = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> IncomingDamageMitigatedLogs = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> OutgoingHealingLogs = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> IncomingHealingLogs = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> ShieldingProvidedLogs = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public ConcurrentDictionary<Entity, List<ParsedLogEntry>> AbilitiesActivated = new ConcurrentDictionary<Entity, List<ParsedLogEntry>>();
        public List<Point> GetBurstValues(Entity entity, PlotType typeOfData)
        {
            var logs = new List<ParsedLogEntry>();
            switch (typeOfData)
            {
                case PlotType.DamageOutput:
                    logs = OutgoingDamageLogs[entity];
                    break;
                case PlotType.HealingOutput:
                    logs = OutgoingHealingLogs[entity];
                    break;
                case PlotType.DamageTaken:
                    logs = IncomingDamageLogs[entity];
                    break;
                case PlotType.HealingTaken:
                    logs = IncomingHealingLogs[entity];
                    break;
            }
            if (logs.Count == 0)
                return new List<Point>();
            var timeStamps = PlotMaker.GetPlotXVals(logs, StartTime);
            var values = logs.Select(l => l.Value.EffectiveDblValue);
            var twentySecondAverage = PlotMaker.GetPlotYValRates(values.ToArray(), timeStamps, 20d);

            var peaks = PlotMaker.GetPeaksOfMean(twentySecondAverage, 20);
            var validPeaks = peaks.Where(p => p.Item1 > 10);
            return validPeaks.Select(p => new Point() { X = p.Item1, Y = p.Item2 }).ToList();

        }
        public double GetCurrentEffectStacks(string effect, Entity target)
        {
            var allEffects = CombatLogStateBuilder.CurrentState.GetEffectsWithTarget(StartTime, EndTime, target);
            if (allEffects.Count == 0) return 0;
            var specificEffect = allEffects.Where(e => e.EffectId == effect || e.Name == effect);
            if (!specificEffect.Any())
                return 0;
            return specificEffect.SelectMany(e => e.ChargesAtTime).MaxBy(v => v.Key).Value;
        }
        public double GetMaxEffectStacks(string effect, Entity target)
        {
            var allEffects = CombatLogStateBuilder.CurrentState.GetEffectsWithTarget(StartTime, EndTime, target);
            if (allEffects.Count == 0) return 0;
            var specificEffect = allEffects.Where(e => e.EffectId == effect || e.Name == effect);
            if (!specificEffect.Any())
                return 0;
            return specificEffect.SelectMany(e => e.ChargesAtTime).MaxBy(v => v.Value).Value;
        }
        public double GetDamageFromEntityByAbilityForPlayer(string ability, string entity, Entity player)
        {
            var incomingDamageByEntity = GetIncomingDamageBySource(player);
            var entityOfInterest = incomingDamageByEntity.Keys.FirstOrDefault(e => e.Name == entity || e.LogId.ToString() == entity);
            if (entityOfInterest != null)
            {
                var logsForEntity = incomingDamageByEntity[entityOfInterest];
                var logsWithAbility = logsForEntity.Where(l => l.Ability == ability || l.AbilityId == ability);
                return logsWithAbility.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public double GetDamageIncomingByAbilityForPlayer(string ability, Entity player)
        {
            return IncomingDamageLogs[player].Where(l => l.Ability == ability || l.AbilityId == ability).Sum(l => l.Value.EffectiveDblValue);
        }
        public double GetDamageToEntityByAbilityForPlayer(string ability, string entity, Entity player)
        {
            var outgoingDamageByEntity = GetOutgoingDamageByTarget(player);
            var entityOfInterest = outgoingDamageByEntity.Keys.FirstOrDefault(e => e.Name == entity || e.LogId.ToString() == entity);
            if (entityOfInterest != null)
            {
                var logsForEntity = outgoingDamageByEntity[entityOfInterest];
                var logsWithAbility = logsForEntity.Where(l => l.Ability == ability || l.AbilityId == ability);
                return logsWithAbility.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public double GetDamageFromEntityByPlayer(string entity, Entity player)
        {
            var incomingDamageByEntity = GetIncomingDamageBySource(player);
            var entityOfInterest = incomingDamageByEntity.Keys.FirstOrDefault(e => e.Name == entity || e.LogId.ToString() == entity);
            if (entityOfInterest != null)
            {
                var logsForEntity = incomingDamageByEntity[entityOfInterest];
                return logsForEntity.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public double GetDamageOutgoingByAbilityForPlayer(string ability, Entity player)
        {
            return OutgoingDamageLogs[player].Where(l => l.Ability == ability || l.AbilityId == ability).Sum(l => l.Value.EffectiveDblValue);
        }
        public double GetDamageToEntityByPlayer(string entity, Entity player)
        {
            var outgoingDamageByEntity = GetOutgoingDamageByTarget(player);
            var entityOfInterest = outgoingDamageByEntity.Keys.FirstOrDefault(e => e.Name == entity || e.LogId.ToString() == entity);
            if (entityOfInterest != null)
            {
                var logsForEntity = outgoingDamageByEntity[entityOfInterest];
                return logsForEntity.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetOutgoingDamageByTarget(Entity source)
        {
            return GetByTarget(OutgoingDamageLogs[source]);
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetIncomingDamageBySource(Entity source)
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
        public Dictionary<Entity, List<ParsedLogEntry>> GetIncomingHealingBySource(Entity source)
        {
            return GetBySource(IncomingHealingLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetIncomingHealingByAbility(Entity source)
        {
            return GetByAbility(IncomingHealingLogs[source]);
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetOutgoingHealingByTarget(Entity source)
        {
            return GetByTarget(OutgoingHealingLogs[source]);
        }
        public Dictionary<string, List<ParsedLogEntry>> GetOutgoingHealingByAbility(Entity source)
        {
            return GetByAbility(OutgoingHealingLogs[source]);
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetShieldingBySource(Entity source)
        {
            return GetBySource(IncomingDamageMitigatedLogs[source]);
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetByTarget(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<Entity, List<ParsedLogEntry>>();
            var distinctTargets = logsToCheck.Select(l => l.Target).Where(v => v.Name != null).DistinctBy(e => e.LogId);
            foreach (var target in distinctTargets)
            {
                returnDict[target] = logsToCheck.Where(l => l.Target.LogId == target.LogId).ToList();
            }
            return returnDict;
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetBySource(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<Entity, List<ParsedLogEntry>>();
            var distinctSources = logsToCheck.Select(l => l.Source).Where(v => v.Name != null).DistinctBy(e => e.LogId);
            foreach (var source in distinctSources)
            {
                returnDict[source] = logsToCheck.Where(l => l.Source.LogId == source.LogId).ToList();
            }
            return returnDict;
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetByTargetName(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<Entity, List<ParsedLogEntry>>();
            var distinctTargets = logsToCheck.Select(l => l.Target).Where(v => v.Name != null).DistinctBy(e => e.Name);
            foreach (var target in distinctTargets)
            {
                returnDict[target] = logsToCheck.Where(l => l.Target.Name == target.Name).ToList();
            }
            return returnDict;
        }
        public Dictionary<Entity, List<ParsedLogEntry>> GetBySourceName(List<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<Entity, List<ParsedLogEntry>>();
            var distinctSources = logsToCheck.Select(l => l.Source).Where(v => v.Name != null).DistinctBy(e => e.Name);
            foreach (var source in distinctSources)
            {
                returnDict[source] = logsToCheck.Where(l => l.Source.Name == source.Name).ToList();
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
        public bool HasBurstValues()
        {
            return AllBurstDamages.Any();
        }
        public void SetBurstValues()
        {
            List<Task> tasks = new List<Task>();
            tasks.Add(Task.Run(() => { SetBurstDamage(); }));
            tasks.Add(Task.Run(() => { SetBurstDamageTaken(); }));
            tasks.Add(Task.Run(() => { SetBurstHealing(); }));
            tasks.Add(Task.Run(() => { SetBurstHealingTaken(); }));
            tasks.ForEach(task => task.Wait());
        }
        public void SetBurstDamage()
        {
            AllBurstDamages = CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.DamageOutput));
        }
        public void SetBurstDamageTaken()
        {
            AllBurstDamageTakens = CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.DamageTaken));
        }
        public void SetBurstHealing()
        {
            AllBurstHealings = CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.HealingOutput));
        }
        public void SetBurstHealingTaken()
        {
            AllBurstHealingReceived = CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.HealingTaken));
        }
        public ConcurrentDictionary<Entity, double> AverageDamageSavedDuringCooldown = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalAbilites = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, long> TotalThreat = new ConcurrentDictionary<Entity, long>();
        public Dictionary<Entity, List<Point>> AllBurstDamages { get; set; } = new Dictionary<Entity, List<Point>>();
        public Dictionary<Entity, double> MaxBurstDamage => AllBurstDamages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public Dictionary<Entity, double> TotalDamage => TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value + TotalFocusDamage[kvp.Key]);
        public ConcurrentDictionary<Entity, double> TotalFluffDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalFocusDamage = new ConcurrentDictionary<Entity, double>();
        public Dictionary<Entity, double> TotalEffectiveDamage => TotalEffectiveFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value + TotalEffectiveFocusDamage[kvp.Key]);
        public ConcurrentDictionary<Entity, double> TotalEffectiveFluffDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalEffectiveFocusDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalCompanionDamage = new ConcurrentDictionary<Entity, double>();
        public Dictionary<Entity, List<Point>> AllBurstHealings { get; set; } = new Dictionary<Entity, List<Point>>();
        public Dictionary<Entity, double> MaxBurstHeal => AllBurstHealings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public ConcurrentDictionary<Entity, double> TotalHealing = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalCompanionHealing = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalEffectiveHealing = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalEffectiveCompanionHealing = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalTankSheilding = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalProvidedSheilding = new ConcurrentDictionary<Entity, double>();
        public Dictionary<Entity, List<Point>> AllBurstDamageTakens { get; set; } = new Dictionary<Entity, List<Point>>();
        public Dictionary<Entity, double> MaxBurstDamageTaken => AllBurstDamageTakens.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public ConcurrentDictionary<Entity, double> TotalDamageTaken = new ConcurrentDictionary<Entity, double>();

        public Dictionary<Entity, List<Point>> AllBurstHealingReceived { get; set; } = new Dictionary<Entity, List<Point>>();
        public Dictionary<Entity, double> MaxBurstHealingReceived => AllBurstHealingReceived.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public Dictionary<Entity, double> CurrentHealthDeficit => TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => Math.Max(0, TotalEffectiveDamageTaken[kvp.Key] - TotalEffectiveHealingReceived[kvp.Key]));
        public ConcurrentDictionary<Entity, double> TimeSpentBelowFullHealth = new ConcurrentDictionary<Entity, double>();
        public Dictionary<Entity, Dictionary<Entity, List<double>>> AllDamageRecoveryTimes = new Dictionary<Entity, Dictionary<Entity, List<double>>>();
        public Dictionary<Entity, Dictionary<Entity, List<double>>> TankDamageRecoveryTimes = new Dictionary<Entity, Dictionary<Entity, List<double>>>();
        public Dictionary<Entity, Dictionary<Entity, double>> AverageDamageRecoveryTimePerTarget => GetDamageRecoveryTimesPerTarget();
        public Dictionary<Entity, Dictionary<Entity, double>> NumberOfFastResponseTimePerTarget => GetCountOfHighSpeedReactions();
        public Dictionary<Entity, Dictionary<Entity, double>> AverageTankDamageRecoveryTimePerTarget => GetTankDamageRecoveryTimesPerTarget();
        private Dictionary<Entity, Dictionary<Entity, double>> GetTankDamageRecoveryTimesPerTarget()
        {
            Dictionary<Entity, Dictionary<Entity, double>> returnDict = new Dictionary<Entity, Dictionary<Entity, double>>();
            foreach (var player in CharacterParticipants)
            {
                if (TankDamageRecoveryTimes.ContainsKey(player))
                {
                    returnDict[player] = TankDamageRecoveryTimes[player].ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Any(v => !double.IsNaN(v)) ? kvp.Value.Where(v => !double.IsNaN(v)).Average() : double.NaN);
                }
                else
                {
                    returnDict[player] = new Dictionary<Entity, double>();
                }
            }
            return returnDict;
        }
        private Dictionary<Entity, Dictionary<Entity, double>> GetDamageRecoveryTimesPerTarget()
        {
            Dictionary<Entity, Dictionary<Entity, double>> returnDict = new Dictionary<Entity, Dictionary<Entity, double>>();
            foreach (var player in CharacterParticipants)
            {
                if (AllDamageRecoveryTimes.ContainsKey(player))
                {
                    returnDict[player] = AllDamageRecoveryTimes[player].ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Any(v => !double.IsNaN(v)) ? kvp.Value.Where(v => !double.IsNaN(v)).Average() : double.NaN);
                }
                else
                {
                    returnDict[player] = new Dictionary<Entity, double>();
                }
            }
            return returnDict;
        }
        private Dictionary<Entity, Dictionary<Entity, double>> GetCountOfHighSpeedReactions()
        {
            var minReactionTime = 2;
            Dictionary<Entity, Dictionary<Entity, double>> returnDict = new Dictionary<Entity, Dictionary<Entity, double>>();
            foreach (var player in CharacterParticipants)
            {
                if (AllDamageRecoveryTimes.ContainsKey(player))
                {
                    returnDict[player] = AllDamageRecoveryTimes[player].ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Any(v => !double.IsNaN(v)) ? kvp.Value.Count(c => !double.IsNaN(c) && c < minReactionTime) : double.NaN);
                }
                else
                {
                    returnDict[player] = new Dictionary<Entity, double>();
                }
            }
            return returnDict;
        }
        public Dictionary<Entity, double> AverageDamageRecoveryTimeTotal => GetTotalDamageRecoveryTimes();

        private Dictionary<Entity, double> GetTotalDamageRecoveryTimes()
        {
            Dictionary<Entity, double> returnDict = new Dictionary<Entity, double>();
            foreach (var player in CharacterParticipants)
            {

                returnDict[player] = AverageDamageRecoveryTimePerTarget[player].Any(kvp => !double.IsNaN(kvp.Value)) ? AverageDamageRecoveryTimePerTarget[player].Where(kvp => !double.IsNaN(kvp.Value)).Average(
                    kvp => kvp.Value) : 0;

            }
            return returnDict;
        }
        public Dictionary<Entity, double> NumberOfHighSpeedReactions => GetTotalHighSpeedReactions();

        private Dictionary<Entity, double> GetTotalHighSpeedReactions()
        {
            Dictionary<Entity, double> returnDict = new Dictionary<Entity, double>();
            foreach (var player in CharacterParticipants)
            {

                returnDict[player] = NumberOfFastResponseTimePerTarget[player].Any(kvp => !double.IsNaN(kvp.Value)) ? NumberOfFastResponseTimePerTarget[player].Where(kvp => !double.IsNaN(kvp.Value)).Sum(
                    kvp => kvp.Value) : 0;

            }
            return returnDict;
        }
        public Dictionary<Entity, double> AverageTankDamageRecoveryTimeTotal => GetTotalTankDamageRecoveryTimes();

        private Dictionary<Entity, double> GetTotalTankDamageRecoveryTimes()
        {
            Dictionary<Entity, double> returnDict = new Dictionary<Entity, double>();
            foreach (var player in CharacterParticipants)
            {

                returnDict[player] = AverageTankDamageRecoveryTimePerTarget[player].Any(kvp => !double.IsNaN(kvp.Value)) ? AverageTankDamageRecoveryTimePerTarget[player].Where(kvp => !double.IsNaN(kvp.Value)).Average(
                    kvp => kvp.Value) : 0;

            }
            return returnDict;
        }

        public ConcurrentDictionary<Entity, double> TotalEffectiveDamageTaken = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalHealingReceived = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalEffectiveHealingReceived = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalInterrupts = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, List<DateTime>> BigDamageTimestamps = new ConcurrentDictionary<Entity, List<DateTime>>();
        public ConcurrentDictionary<Entity, double> TotalSheildAndAbsorb = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> TotalEstimatedAvoidedDamage = new ConcurrentDictionary<Entity, double>();
        public Dictionary<Entity, double> CritPercent => OutgoingDamageLogs.ToDictionary(kvp => kvp.Key, kvp => (OutgoingHealingLogs[kvp.Key].Count(d => d.Value.WasCrit) + kvp.Value.Count(d => d.Value.WasCrit)) / (double)(kvp.Value.Count() + OutgoingHealingLogs[kvp.Key].Count()));
        public Dictionary<Entity, double> DamageSavedFromCDPerSecond => DurationSeconds == 0 ? AverageDamageSavedDuringCooldown.ToDictionary(kvp => kvp.Key, kvp => 0d) : AverageDamageSavedDuringCooldown.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> MitigationPercent => TotalDamageTaken.ToDictionary(kvp => kvp.Key, kvp => kvp.Value == 0 ? 0 : (EstimatedTotalMitigation[kvp.Key] / kvp.Value) * 100);
        public Dictionary<Entity, double> EstimatedTotalMitigation => TotalSheildAndAbsorb.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value + TotalEstimatedAvoidedDamage[kvp.Key]));
        public Dictionary<Entity, double> PercentageOfFightBelowFullHP => DurationSeconds == 0 ? TimeSpentBelowFullHealth.ToDictionary(kvp => kvp.Key, kvp => 0d) : TimeSpentBelowFullHealth.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value / DurationSeconds) * 100);
        public Dictionary<Entity, long> TPS => DurationSeconds == 0 ? TotalThreat.ToDictionary(kvp => kvp.Key, kvp => 0L) : TotalThreat.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> DPS => DurationSeconds == 0 ? TotalDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EDPS => DurationSeconds == 0 ? TotalEffectiveDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> RegDPS => DurationSeconds == 0 ? TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> FocusDPS => DurationSeconds == 0 ? TotalFocusDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalFocusDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> ERegDPS => DurationSeconds == 0 ? TotalEffectiveFluffDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EFocusDPS => DurationSeconds == 0 ? TotalEffectiveFocusDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveFocusDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> CompDPS => DurationSeconds == 0 ? TotalCompanionDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalCompanionDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> APM => DurationSeconds == 0 ? TotalAbilites.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalAbilites.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / (DurationSeconds / 60d));
        public Dictionary<Entity, double> HPS => DurationSeconds == 0 ? TotalHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EHPS => DurationSeconds == 0 ? TotalEffectiveHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> CompEHPS => DurationSeconds == 0 ? TotalEffectiveCompanionHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveCompanionHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> SPS => DurationSeconds == 0 ? TotalTankSheilding.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalTankSheilding.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> PSPS => DurationSeconds == 0 ? TotalProvidedSheilding.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalProvidedSheilding.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> DTPS => DurationSeconds == 0 ? TotalDamageTaken.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalDamageTaken.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> SAPS => DurationSeconds == 0 ? TotalSheildAndAbsorb.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalSheildAndAbsorb.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> DAPS => DurationSeconds == 0 ? TotalEstimatedAvoidedDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEstimatedAvoidedDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> MPS => DurationSeconds == 0 ? EstimatedTotalMitigation.ToDictionary(kvp => kvp.Key, kvp => 0d) : EstimatedTotalMitigation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EDTPS => DurationSeconds == 0 ? TotalEffectiveDamageTaken.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveDamageTaken.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> HTPS => DurationSeconds == 0 ? TotalHealingReceived.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalHealingReceived.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EHTPS => DurationSeconds == 0 ? TotalEffectiveHealingReceived.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveHealingReceived.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);

        public ConcurrentDictionary<Entity, double> MaxDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxEffectiveDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxIncomingDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxEffectiveIncomingDamage = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxHeal = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxEffectiveHeal = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxIncomingHeal = new ConcurrentDictionary<Entity, double>();
        public ConcurrentDictionary<Entity, double> MaxIncomingEffectiveHeal = new ConcurrentDictionary<Entity, double>();
        public string Serialize()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
