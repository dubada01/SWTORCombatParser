using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;

namespace SWTORCombatParser.DataStructures
{
    public class RichAbilityComparer : IEqualityComparer<RichAbility>
    {
        public bool Equals(RichAbility x, RichAbility y)
        {
            if (x == null || y == null)
                return false;

            return x.AbilityId == y.AbilityId && (x.AbilitySource.LogId == y.AbilitySource.LogId || x.AbilitySource.IsCharacter);
        }

        public int GetHashCode(RichAbility obj)
        {
            if (obj == null)
                return 0;

            // Combine the hash codes of the properties you want to compare.
            if (obj.AbilitySource.IsCharacter)
            {
                return obj.AbilityId.GetHashCode();
            }
            return HashCode.Combine(obj.AbilityId, obj.AbilitySource.LogId);
        }
    }
    public class RichAbility
    {
        public string AbilityName { get; set; }
        public ulong AbilityId { get; set; }
        public Entity AbilitySource { get; set; }
    }
    public class Combat
    {
        public Entity Initiator { get; set; }
    
        public Entity LocalPlayer => CharacterParticipants.FirstOrDefault(p => p.IsLocalPlayer);
        public List<Entity> CharacterParticipants = new();
        public Dictionary<Entity, SWTORClass> CharacterClases = new();
        public List<Entity> Targets = new();
        public List<Entity> AllEntities => new List<Entity>().Concat(Targets).Concat(CharacterParticipants).ToList();
        public DateTime StartTime;
        public DateTime EndTime;
        public string LogFileName => AllLogs.Values.First(l => !string.IsNullOrEmpty(l.LogName)).LogName;
        public double DurationOverride { get; set; }
        public double DurationMS => DurationOverride == 0 ? (PhaseManager.SelectedPhases.Any() ? PhaseManager.PhaseDuration : (EndTime - StartTime).TotalMilliseconds) : DurationOverride;
        public int DurationSeconds => (int)Math.Round(DurationMS / 1000f);


        public EncounterInfo.EncounterInfo ParentEncounter;
        public BossInfo BossInfo;
        public string EncounterBossInfo => EncounterBossDifficultyParts == ("", "", "") ? "" : $"{EncounterBossDifficultyParts.Item1} {{{EncounterBossDifficultyParts.Item2} {EncounterBossDifficultyParts.Item3}}}";
        public string OldFlashpointBossInfo => EncounterBossDifficultyParts == ("", "", "") ? "" : $"{EncounterBossDifficultyParts.Item1} {{{EncounterBossDifficultyParts.Item3}}}";
        public (string, string, string) EncounterBossDifficultyParts = ("", "", "");

        public List<long> RequiredDeadTargetsForKill => BossInfo.TargetsRequiredForKill;
        public ulong RequiredAbilityForKill => BossInfo.AbilityRequiredForKill;
        public bool IsCombatWithBoss => !string.IsNullOrEmpty(EncounterBossInfo);
        public bool IsPvPCombat => Targets.Any(t => t.IsCharacter) && CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(StartTime).IsPvpEncounter;
        public bool BossKillOverride { get; set; }
        public bool WasBossKilled
        {
            get
            {
                if (BossKillOverride)
                    return true;
                if (RequiredDeadTargetsForKill.Count > 0)
                {
                    if(BossInfo.IsOpenWorld)
                    {
                        // Check if all required targets are killed using efficient HashSet lookup
                        var openWorldBosses = new HashSet<long>(RequiredDeadTargetsForKill);
                        if(AllLogs.Values.Where(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId)
                        .Select(l => l.Target.LogId).Any(kill => openWorldBosses.Contains(kill)))
                        {
                            return true;
                        }
                    }
                    // Check if all required targets are killed using efficient HashSet lookup
                    var killedTargetsSet = new HashSet<long>(RequiredDeadTargetsForKill);
                    if (killedTargetsSet.IsSubsetOf(AllLogs.Values.Where(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId)
                        .Select(l => l.Target.LogId)))
                    {
                        return true;
                    }
                }

                if (RequiredAbilityForKill != 0)
                {
                    // Check if at least one log contains the required ability
                    if (AllLogs.Values.Any(l => l.AbilityId == RequiredAbilityForKill))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        public ConcurrentDictionary<long,ParsedLogEntry> AllLogs { get; set; } = new();
        public Dictionary<long, ConcurrentQueue<ParsedLogEntry>> LogsInvolvingEntity = new();

        public ConcurrentQueue<ParsedLogEntry> GetLogsInvolvingEntity(Entity e)
        {
            if (string.IsNullOrEmpty(e.Name) || !LogsInvolvingEntity.TryGetValue(e.LogId, out var entity))
            {
                return new ConcurrentQueue<ParsedLogEntry>();
            }

            return entity;
        }
        public bool WasPlayerKilled(Entity player)
        {
            return GetLogsInvolvingEntity(player).Any(l => l.Target == player && l.Effect.EffectId == _7_0LogParsing.DeathCombatId);
        }
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> OutgoingDamageLogs = new();
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> IncomingDamageLogs = new();
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> IncomingDamageMitigatedLogs = new();
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> OutgoingHealingLogs = new();
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> IncomingHealingLogs = new();
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> ShieldingProvidedLogs = new();
        public ConcurrentDictionary<Entity, ConcurrentQueue<ParsedLogEntry>> AbilitiesActivated = new();
        public ConcurrentDictionary<Entity, Dictionary<Entity, double>> PlayerThreatPerEnemy  = new();
        public List<Point> GetBurstValues(Entity entity, PlotType typeOfData)
        {
            var logs = new ConcurrentQueue<ParsedLogEntry>();
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
            return validPeaks.Select(p => new Point(p.Item1,p.Item2)).ToList();

        }
        public double GetCurrentEffectStacks(ulong effect, Entity target)
        {
            var allEffects = CombatLogStateBuilder.CurrentState.GetEffectsWithTarget(StartTime, EndTime, target);
            if (allEffects.Count == 0) return 0;
            var specificEffect = allEffects.Where(e => e.EffectId == effect || (long.TryParse(e.Name, out var _) && ulong.Parse(e.Name) == effect));
            if (!specificEffect.Any())
                return 0;
            return specificEffect.SelectMany(e => e.ChargesAtTime).MaxBy(v => v.Key).Value;
        }
        public double GetMaxEffectStacks(ulong effect, Entity target)
        {
            var allEffects = CombatLogStateBuilder.CurrentState.GetEffectsWithTarget(StartTime, EndTime, target);
            if (allEffects.Count == 0) return 0;
            var specificEffect = allEffects.Where(e => e.EffectId == effect || (long.TryParse(e.Name, out var _) && ulong.Parse(e.Name) == effect));
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
                var logsWithAbility = ulong.TryParse(ability, out var abilityId)
                    ? logsForEntity.Where(l => l.Ability == ability || l.AbilityId == abilityId)
                    : logsForEntity.Where(l => l.Ability == ability);
                return logsWithAbility.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public double GetDamageIncomingByAbilityForPlayer(string ability, Entity player)
        {
            var logsWithAbility = ulong.TryParse(ability, out var abilityId)
                ? IncomingDamageLogs[player].Where(l => l.Ability == ability || l.AbilityId == abilityId)
                : IncomingDamageLogs[player].Where(l => l.Ability == ability);
            return logsWithAbility.Sum(l => l.Value.EffectiveDblValue);
        }
        public double GetDamageIncomingByAbilityForPlayerFromSource(string ability, Entity player, Entity source)
        {
            var logsWithAbility = ulong.TryParse(ability, out var abilityId)
                ? IncomingDamageLogs[player].Where(l => l.Ability == ability || l.AbilityId == abilityId)
                : IncomingDamageLogs[player].Where(l => l.Ability == ability);
            return logsWithAbility.Where(l => l.Source == source).Sum(l => l.Value.EffectiveDblValue);
        }
        public double GetDamageToEntityByAbilityForPlayer(string ability, string entity, Entity player)
        {
            var outgoingDamageByEntity = GetOutgoingDamageByTarget(player);
            var entityOfInterest = outgoingDamageByEntity.Keys.FirstOrDefault(e => e.Name == entity || e.LogId.ToString() == entity);
            if (entityOfInterest != null)
            {
                var logsForEntity = outgoingDamageByEntity[entityOfInterest];
                var logsWithAbility = ulong.TryParse(ability, out var abilityId)
                    ? logsForEntity.Where(l => l.Ability == ability || l.AbilityId == abilityId)
                    : logsForEntity.Where(l => l.Ability == ability);
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
            var logsWithAbility = ulong.TryParse(ability, out var abilityId)
                ? OutgoingDamageLogs[player].Where(l => l.Ability == ability || l.AbilityId == abilityId)
                : OutgoingDamageLogs[player].Where(l => l.Ability == ability);
            return logsWithAbility.Sum(l => l.Value.EffectiveDblValue);
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
        public double GetMaxTotalDamageToSingleTargetByPlayer(Entity player)
        {
            var outgoingDamageByEntity = GetOutgoingDamageByTarget(player);
            if (outgoingDamageByEntity.Any())
            {
                var target = outgoingDamageByEntity.MaxBy(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
                return target.Value.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public double GetMaxTotalHealingToSingleTargetByPlayer(Entity player)
        {
            var outgoingDamageByEntity = GetOutgoingHealingByTarget(player);
            if (outgoingDamageByEntity.Any())
            {
                var target = outgoingDamageByEntity.MaxBy(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
                return target.Value.Sum(v => v.Value.EffectiveDblValue);
            }
            return 0;
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetOutgoingDamageByTarget(Entity source)
        {
            return GetByTarget(OutgoingDamageLogs[source]);
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetIncomingDamageBySource(Entity source)
        {
            return GetBySource(IncomingDamageLogs[source]);
        }
        public Dictionary<string, ConcurrentQueue<ParsedLogEntry>> GetOutgoingDamageByAbility(Entity source)
        {
            return GetByAbility(OutgoingDamageLogs[source]);
        }
        public Dictionary<string, ConcurrentQueue<ParsedLogEntry>> GetIncomingDamageByAbility(Entity source)
        {
            return GetByAbility(IncomingDamageLogs[source]);
        }
        public Dictionary<RichAbility, ConcurrentQueue<ParsedLogEntry>> GetIncomingDamageByAbilityRich(Entity source)
        {
            return GetByAbilityRich(IncomingDamageLogs[source]);
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetIncomingHealingBySource(Entity source)
        {
            return GetBySource(IncomingHealingLogs[source]);
        }
        public Dictionary<string, ConcurrentQueue<ParsedLogEntry>> GetIncomingHealingByAbility(Entity source)
        {
            return GetByAbility(IncomingHealingLogs[source]);
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetOutgoingHealingByTarget(Entity source)
        {
            return GetByTarget(OutgoingHealingLogs[source]);
        }
        public Dictionary<string, ConcurrentQueue<ParsedLogEntry>> GetOutgoingHealingByAbility(Entity source)
        {
            return GetByAbility(OutgoingHealingLogs[source]);
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetShieldingBySource(Entity source)
        {
            return GetBySource(IncomingDamageMitigatedLogs[source]);
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetByTarget(IEnumerable<ParsedLogEntry> logsToCheck)
        {
                var returnDict = new Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>>();
                var distinctTargets = logsToCheck.Select(l => l.Target).Where(v => v.Name != null).DistinctBy(e => e.LogId);
                foreach (var target in distinctTargets)
                {
                    returnDict[target] = new ConcurrentQueue<ParsedLogEntry>(logsToCheck.Where(l => l.Target.LogId == target.LogId));
                }
                return returnDict;
            
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetBySource(IEnumerable<ParsedLogEntry> logsToCheck)
        {
                var returnDict = new Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>>();
                var distinctSources = logsToCheck.Select(l => l.Source).Where(v => v.Name != null)
                    .DistinctBy(e => e.LogId);
                foreach (var source in distinctSources)
                {
                    returnDict[source] = new ConcurrentQueue<ParsedLogEntry>(logsToCheck.Where(l => l.Source.LogId == source.LogId));
                }

                return returnDict;
            
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetByTargetName(IEnumerable<ParsedLogEntry> logsToCheck)
        {
                var returnDict = new Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>>();
                var distinctTargets = logsToCheck.Select(l => l.Target).Where(v => v.Name != null)
                    .DistinctBy(e => e.Name);
                foreach (var target in distinctTargets)
                {
                    returnDict[target] = new ConcurrentQueue<ParsedLogEntry>(logsToCheck.Where(l => l.Target.Name == target.Name));
                }

                return returnDict;
        }
        public Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> GetBySourceName(IEnumerable<ParsedLogEntry> logsToCheck)
        {
            var returnDict = new Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>>();
            var distinctSources = logsToCheck.Select(l => l.Source).Where(v => v.Name != null).DistinctBy(e => e.Name);
            foreach (var source in distinctSources)
            {
                returnDict[source] = new ConcurrentQueue<ParsedLogEntry>(logsToCheck.Where(l => l.Source.Name == source.Name));
            }
            return returnDict;
        }
        public Dictionary<string, ConcurrentQueue<ParsedLogEntry>> GetByAbility(IEnumerable<ParsedLogEntry> logsToCheck)
        {
                var returnDict = new Dictionary<string, ConcurrentQueue<ParsedLogEntry>>();
                var distinctAbilities = logsToCheck.Select(l => l.Ability).Distinct();
                foreach (var ability in distinctAbilities)
                {
                    returnDict[ability] = new ConcurrentQueue<ParsedLogEntry>(logsToCheck.Where(l => l.Ability == ability));
                }

                return returnDict;
            
        }
        public Dictionary<RichAbility, ConcurrentQueue<ParsedLogEntry>> GetByAbilityRich(ConcurrentQueue<ParsedLogEntry> logsToCheck)
        {
                var returnDict = new Dictionary<RichAbility, ConcurrentQueue<ParsedLogEntry>>();
                var distinctAbilities = logsToCheck
                    .Select(l => new RichAbility()
                        { AbilityId = l.AbilityId, AbilitySource = l.Source, AbilityName = l.Ability }).DistinctBy(ra =>
                        !ra.AbilitySource.IsCharacter ? ra.AbilityId + ra.AbilitySource.Name : ra.AbilityId.ToString());
                foreach (var ability in distinctAbilities)
                {
                    returnDict[ability] = new ConcurrentQueue<ParsedLogEntry>(logsToCheck
                        .Where(l => l.AbilityId == ability.AbilityId && l.Source == ability.AbilitySource));
                }

                return returnDict;
        }
        public bool HasBurstValues()
        {
            return AllBurstDamages.Any();
        }
        public void SetBurstValues()
        {
            List<Task> tasks =
            [
                Task.Run(SetBurstDamage),
                Task.Run(SetBurstDamageTaken),
                Task.Run(SetBurstHealing),
                Task.Run(SetBurstHealingTaken)
            ];
            Task.WaitAll(tasks.ToArray());
        }
        public void SetBurstDamage()
        {
            AllBurstDamages = new ConcurrentDictionary<Entity, List<Point>>(CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.DamageOutput)));
        }
        public void SetBurstDamageTaken()
        {
            AllBurstDamageTakens = new ConcurrentDictionary<Entity, List<Point>>(CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.DamageTaken)));
        }
        public void SetBurstHealing()
        {
            AllBurstHealings = new ConcurrentDictionary<Entity, List<Point>>(CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.HealingOutput)));
        }
        public void SetBurstHealingTaken()
        {
            AllBurstHealingReceived = new ConcurrentDictionary<Entity, List<Point>>(CharacterParticipants.ToDictionary(player => player, player => GetBurstValues(player, PlotType.HealingTaken)));
        }
        public ConcurrentDictionary<Entity, double> AverageDamageSavedDuringCooldown = new();
        public ConcurrentDictionary<Entity, double> TotalAbilites = new();
        public ConcurrentDictionary<Entity, double> TotalThreat = new();
        public ConcurrentDictionary<Entity, List<Point>> AllBurstDamages { get; set; } = new();
        public ConcurrentDictionary<Entity, double> MaxBurstDamage => new( AllBurstDamages.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y)));
        public ConcurrentDictionary<Entity, double> TotalDamage => new(TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value + TotalFocusDamage[kvp.Key]));
        public Dictionary<Entity, double> MaxSingleTargetDamage => TotalDamage.ToDictionary(kvp => kvp.Key, kvp => GetMaxTotalDamageToSingleTargetByPlayer(kvp.Key));
        public ConcurrentDictionary<Entity, double> TotalFluffDamage = new();
        public ConcurrentDictionary<Entity, double> TotalFocusDamage = new();
        public Dictionary<Entity, double> TotalEffectiveDamage => TotalEffectiveFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value + TotalEffectiveFocusDamage[kvp.Key]);
        public ConcurrentDictionary<Entity, double> TotalEffectiveFluffDamage = new();
        public ConcurrentDictionary<Entity, double> TotalEffectiveFocusDamage = new();
        public ConcurrentDictionary<Entity, double> TotalCompanionDamage = new();
        public ConcurrentDictionary<Entity, List<Point>> AllBurstHealings { get; set; } = new();
        public Dictionary<Entity, double> MaxBurstHeal => AllBurstHealings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public ConcurrentDictionary<Entity, double> TotalHealing = new();
        public Dictionary<Entity, double> MaxSingleTargetHealing => TotalHealing.ToDictionary(kvp => kvp.Key, kvp => GetMaxTotalHealingToSingleTargetByPlayer(kvp.Key));
        public ConcurrentDictionary<Entity, double> TotalCompanionHealing = new();
        public ConcurrentDictionary<Entity, double> TotalEffectiveHealing = new();
        public ConcurrentDictionary<Entity, double> TotalEffectiveCompanionHealing = new();
        public ConcurrentDictionary<Entity, double> TotalTankSheilding = new();
        public ConcurrentDictionary<Entity, double> TotalProvidedSheilding = new();
        public ConcurrentDictionary<Entity, List<Point>> AllBurstDamageTakens { get; set; } = new();
        public Dictionary<Entity, double> MaxBurstDamageTaken => AllBurstDamageTakens.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public ConcurrentDictionary<Entity, double> TotalDamageTaken = new();

        public ConcurrentDictionary<Entity, List<Point>> AllBurstHealingReceived { get; set; } = new();
        public Dictionary<Entity, double> MaxBurstHealingReceived => AllBurstHealingReceived.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count == 0 ? 0 : kvp.Value.Max(v => v.Y));
        public Dictionary<Entity, double> CurrentHealthDeficit => TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => Math.Max(0, TotalEffectiveDamageTaken[kvp.Key] - TotalEffectiveHealingReceived[kvp.Key]));
        public ConcurrentDictionary<Entity, double> TimeSpentBelowFullHealth = new();
        public Dictionary<Entity, Dictionary<Entity, List<double>>> AllDamageRecoveryTimes = new();
        public Dictionary<Entity, Dictionary<Entity, List<double>>> TankDamageRecoveryTimes = new();
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

        public ConcurrentDictionary<Entity, double> TotalEffectiveDamageTaken = new();
        public ConcurrentDictionary<Entity, double> TotalHealingReceived = new();
        public ConcurrentDictionary<Entity, double> TotalEffectiveHealingReceived = new();
        public ConcurrentDictionary<Entity, double> TotalInterrupts = new();
        public ConcurrentDictionary<Entity, double> AverageCleanseSpeed = new();
        public ConcurrentDictionary<Entity, double> TotalCleanses = new();
        public ConcurrentDictionary<Entity, List<DateTime>> BigDamageTimestamps = new();
        public ConcurrentDictionary<Entity, double> TotalSheildAndAbsorb = new();
        public ConcurrentDictionary<Entity, double> TotalEstimatedAvoidedDamage = new();
        public Dictionary<Entity, double> CritPercent => OutgoingDamageLogs.ToDictionary(kvp => kvp.Key, kvp => (OutgoingHealingLogs[kvp.Key].Count(d => d.Value.WasCrit) + kvp.Value.Count(d => d.Value.WasCrit)) / (double)(kvp.Value.Count() + OutgoingHealingLogs[kvp.Key].Count()));
        public Dictionary<Entity, double> DamageSavedFromCDPerSecond => DurationSeconds == 0 ? AverageDamageSavedDuringCooldown.ToDictionary(kvp => kvp.Key, kvp => 0d) : AverageDamageSavedDuringCooldown.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> MitigationPercent => TotalDamageTaken.ToDictionary(kvp => kvp.Key, kvp => kvp.Value == 0 ? 0 : (EstimatedTotalMitigation[kvp.Key] / kvp.Value) * 100);
        public Dictionary<Entity, double> EstimatedTotalMitigation => TotalSheildAndAbsorb.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value + TotalEstimatedAvoidedDamage[kvp.Key]));
        public Dictionary<Entity, double> PercentageOfFightBelowFullHP => DurationSeconds == 0 ? TimeSpentBelowFullHealth.ToDictionary(kvp => kvp.Key, kvp => 0d) : TimeSpentBelowFullHealth.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value / DurationSeconds) * 100);
        public Dictionary<Entity, double> TPS => DurationSeconds == 0 ? TotalThreat.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalThreat.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> DPS => DurationSeconds == 0 ? TotalDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> InstantaneousEffectiveDPS
        {
            get
            {
                double windowSeconds = 10.0;
                var startTime = EndTime.AddSeconds(-windowSeconds);

                return OutgoingDamageLogs.ToDictionary(
                    kvp => kvp.Key,
                    kvp =>
                    {
                        var snapshot = kvp.Value.ToArray();
                        if (snapshot.Length == 0) return 0d;

                        // sum only those in the last windowSeconds
                        var total = snapshot
                            .Reverse()
                            .TakeWhile(e => e.TimeStamp >= startTime)
                            .Sum(e => e.Value.EffectiveDblValue);

                        return total / windowSeconds;
                    }
                );
            }
        }
        public Dictionary<Entity, double> STDPS => DurationSeconds == 0 ? MaxSingleTargetDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : MaxSingleTargetDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EDPS => DurationSeconds == 0 ? TotalEffectiveDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> RegDPS => DurationSeconds == 0 ? TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> FocusDPS => DurationSeconds == 0 ? TotalFocusDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalFocusDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> ERegDPS => DurationSeconds == 0 ? TotalEffectiveFluffDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveFluffDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EFocusDPS => DurationSeconds == 0 ? TotalEffectiveFocusDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveFocusDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> CompDPS => DurationSeconds == 0 ? TotalCompanionDamage.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalCompanionDamage.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> APM => DurationSeconds == 0 ? TotalAbilites.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalAbilites.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / (DurationSeconds / 60d));
        public Dictionary<Entity, double> HPS => DurationSeconds == 0 ? TotalHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> EHPS => DurationSeconds == 0 ? TotalEffectiveHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : TotalEffectiveHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
        public Dictionary<Entity, double> InstantaneousEffectiveHPS
        {
            get
            {
                double windowSeconds = 10.0;
                var startTime = EndTime.AddSeconds(-windowSeconds);

                return OutgoingHealingLogs.ToDictionary(
                    kvp => kvp.Key,
                    kvp =>
                    {
                        var entity = kvp.Key;
                        var heals   = kvp.Value.ToArray().Reverse();
                        var shields = ShieldingProvidedLogs.TryGetValue(entity, out var q)
                            ? q.ToArray().Reverse()
                            : Enumerable.Empty<ParsedLogEntry>();

                        var sumHeal   = heals.TakeWhile(e => e.TimeStamp >= startTime).Sum(e => e.Value.EffectiveDblValue);
                        var sumShield = shields.TakeWhile(e => e.TimeStamp >= startTime).Sum(e => e.Value.EffectiveDblValue);

                        return (sumHeal + sumShield) / windowSeconds;
                    }
                );
            }
        }
        public Dictionary<Entity, double> STEHPS => DurationSeconds == 0 ? MaxSingleTargetHealing.ToDictionary(kvp => kvp.Key, kvp => 0d) : MaxSingleTargetHealing.ToDictionary(kvp => kvp.Key, kvp => kvp.Value / DurationSeconds);
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

        public ConcurrentDictionary<Entity, double> MaxDamage = new();
        public ConcurrentDictionary<Entity, double> MaxEffectiveDamage = new();
        public ConcurrentDictionary<Entity, double> MaxIncomingDamage = new();
        public ConcurrentDictionary<Entity, double> MaxEffectiveIncomingDamage = new();
        public ConcurrentDictionary<Entity, double> MaxHeal = new();
        public ConcurrentDictionary<Entity, double> MaxEffectiveHeal = new();
        public ConcurrentDictionary<Entity, double> MaxIncomingHeal = new();
        public ConcurrentDictionary<Entity, double> MaxIncomingEffectiveHeal = new();
        public Combat GetPhaseCopy(ConcurrentDictionary<Guid,PhaseInstance> phases)
        {
            List<ParsedLogEntry> phaseLogs = new List<ParsedLogEntry>();
            var snapshot = AllLogs.ToArray().OrderBy(kvp => kvp.Key);
            foreach (var phase in phases)
            {
                phaseLogs.AddRange(phase.Value.PhaseEnd == DateTime.MinValue
                    ? snapshot.Where(l => l.Value.TimeStamp > phase.Value.PhaseStart).Select(kvp => kvp.Value)
                    : snapshot.Where(l => phase.Value.ContainsTime(l.Value.TimeStamp)).Select(kvp => kvp.Value));
            }

            if (!phaseLogs.Any())
                return new Combat();
            var duration = phases.Sum(p => ((p.Value.PhaseEnd == DateTime.MinValue ? CombatIdentifier.CurrentCombat.EndTime : p.Value.PhaseEnd) - p.Value.PhaseStart).TotalSeconds);
            var phaseCombat = CombatIdentifier.GenerateCombatSnapshotFromLogs(phaseLogs,combatEndUpdate:true);
            var tempDuration = duration * 1000;
            if (tempDuration < phaseCombat.DurationMS)
                phaseCombat.DurationOverride = tempDuration;
            return phaseCombat;
        }
    }
}
