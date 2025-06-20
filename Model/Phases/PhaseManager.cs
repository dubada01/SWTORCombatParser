using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Phases
{
    public enum PhaseTrigger
    {
        Default,
        EntitySpawn,
        EntityDeath,
        EntityHP,
        AbilityUsage,
        AbilityCancel,
        EffectGain,
        EffectLoss,
        CombatDuration,
        CombatStart
    }
    public class PhaseArgs
    {
        public List<long> EntityIds { get; set; } = new List<long>();
        public List<string> AbilityIds { get; set; } = new List<string>();
        public List<string> EffectIds { get; set; } = new List<string>();
        public double HPPercentage { get; set; }
        public double CombatDuration { get; set; }

    }
    public class PhaseInstance
    {
        public Guid Id { get; set; }
        public Phase SourcePhase { get; set; }
        public DateTime PhaseStart { get; set; }
        public DateTime PhaseEnd { get; set; }
        public bool ArtificallyEnded { get; set; }
        internal bool ContainsTime(DateTime timeStamp)
        {
            //this function returns true if the timestamp is within the phase
            return timeStamp >= PhaseStart && timeStamp <= PhaseEnd;
        }
    }
    public class Phase
    {
        public void SetSource()
        {
            SourcePt2 = PhaseSource.Split('|')[1];
        }
        public Guid Id { get; set; }
        public string SourcePt1 { get; set; }
        public string SourcePt2 { get; set; }
        public string PhaseSource { get; set; }
        public string Name { get; set; }
        public PhaseTrigger StartTrigger { get; set; }
        public PhaseTrigger EndTrigger { get; set; }

        public PhaseArgs StartArgs { get; set; }
        public PhaseArgs EndArgs { get; set; }
    }
    public static class PhaseManager
    {
        // Define a dictionary to map PhaseTrigger to delegate
        private static readonly Dictionary<PhaseTrigger, Action<ParsedLogEntry, Phase, bool>> triggerHandlers = new Dictionary<PhaseTrigger, Action<ParsedLogEntry, Phase, bool>>
{
    { PhaseTrigger.CombatStart, HandleCombatStart },
    { PhaseTrigger.EntitySpawn, HandleEntitySpawn },
    { PhaseTrigger.EntityDeath, HandleEntityDeath },
    { PhaseTrigger.EntityHP, HandleEntityHP },
    { PhaseTrigger.AbilityUsage, HandleAbilityUsage },
    { PhaseTrigger.AbilityCancel, HandleAbilityCancel },
    { PhaseTrigger.EffectGain, HandleEffectGain },
    { PhaseTrigger.EffectLoss, HandleEffectLoss },
    { PhaseTrigger.CombatDuration, HandleDuration}
};
        private static HashSet<Entity> _detectedEntities = new HashSet<Entity>();

        private static string _currentBossName;
        private static DateTime _combatStartTime;
        private static bool _combatStartToggled;
        private static Dictionary<Guid, bool> _hpPhasesTriggered = new Dictionary<Guid, bool>();
        private static double phaseDuration;

        private static IEnumerable<Phase> _loadedPhases { get; set; }
        public static ObservableCollection<PhaseInstance> ActivePhases
        {
            get
            {
                return activePhases;
            }
            set
            {
                activePhases = value;
            }
        }
        public static List<PhaseInstance> SelectedPhases { get; set; } = new List<PhaseInstance>();
        public static double PhaseDuration => phaseDuration * 1000f;

        public static void SetSelectedPhaseDuration()
        {
            if (SelectedPhases.Count == 0)
            {
                phaseDuration = 0;
                return;
            }
            // set phaseDuration to the sum of the duration of the selected phases
            phaseDuration = SelectedPhases.Sum(p => ((p.PhaseEnd == DateTime.MinValue ? CombatIdentifier.CurrentCombat.EndTime : p.PhaseEnd) - p.PhaseStart).TotalSeconds);
        }

        public static event Action<List<PhaseInstance>> PhaseInstancesUpdated = delegate { };
        public static event Action<List<PhaseInstance>> SelectedPhasesUpdated = delegate { };
        
        private static ObservableCollection<PhaseInstance> activePhases = new ObservableCollection<PhaseInstance>();

        public static void Init()
        {
            //need to load phases from a .json file that can be edited by the user
            _loadedPhases = DefaultPhaseManager.GetExisitingPhases();
            //need to reset the phases when combat starts
            CombatLogStreamer.CombatUpdated += UpdatePhases;
            CombatSelectionMonitor.CombatSelected += UpdateForSelectedCombat;

            PhaseListViewModel.PhasesUpdated += () =>
            {
                _loadedPhases = DefaultPhaseManager.GetExisitingPhases();
            };

            EncounterTimerTrigger.BossCombatDetected += SetBossInfo;
        }
        private static void SetBossInfo(string encounterName, string bossName, string difficulty)
        {
            _currentBossName = bossName;
        }
        private static object _selectionLock = new object();
        public static void UpdateForSelectedCombat(Combat combat)
        {
            lock (_selectionLock)
            {
                _loadedPhases = DefaultPhaseManager.GetExisitingPhases();
                ResetPhases();
                _combatStartTime = combat.StartTime;
                _currentBossName = combat.EncounterBossDifficultyParts.Item1;
                foreach (var line in combat.AllLogs.ToArray().OrderBy(l=>l.Key))
                {
                    HandleNewLine(line.Value);
                }
                PhaseInstancesUpdated.InvokeSafely(ActivePhases.ToList());
            }
        }
        private static void UpdatePhases(CombatStatusUpdate update)
        {
            lock (_selectionLock)
            {
                if (update.Type == UpdateType.Start)
                {
                    ResetPhases();
                    _combatStartTime = update.CombatStartTime;
                }
                if (update.Logs != null && update.Logs.Count > 0)
                {
                    foreach (var line in update.Logs.OrderBy(l=>l.LogLineNumber))
                    {
                        HandleNewLine(line);
                    }
                }
                PhaseInstancesUpdated.InvokeSafely(ActivePhases.ToList());
            }
        }
        private static void UpdateActiveEntities(ParsedLogEntry entry)
        {
            _detectedEntities.Add(entry.Source);
            if (entry.Effect.EffectId == _7_0LogParsing.DeathCombatId)
            {
                _detectedEntities.RemoveWhere(x => x.LogId == entry.Target.LogId);
            }
        }

        private static void ResetPhases()
        {
            _combatStartToggled = false;
            _detectedEntities.Clear();
            ActivePhases.Clear();
            SelectedPhases.Clear();
            _hpPhasesTriggered.Clear();
            SetSelectedPhaseDuration();
        }

        private static void HandleNewLine(ParsedLogEntry entry)
        {
            foreach (var phase in _loadedPhases.Where(p => p.SourcePt2 == _currentBossName))
            {
                if (triggerHandlers.TryGetValue(phase.StartTrigger, out var startTrigger))
                {
                    startTrigger(entry, phase, true);
                }

                if (triggerHandlers.TryGetValue(phase.EndTrigger, out var endTrigger))
                {
                    endTrigger(entry, phase, false);
                }
            }
            UpdateActiveEntities(entry);
        }
        private static void HandleCombatStart(ParsedLogEntry entry, Phase phase, bool starting)
        {
            if (!_combatStartToggled)
            {
                _combatStartToggled = true;
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void HandleEntitySpawn(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var activaePhaseOfSameType = ActivePhases.FirstOrDefault(p => p.SourcePhase.Id == phase.Id && p.PhaseEnd == DateTime.MinValue);
            if (activaePhaseOfSameType != null && starting)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if (!_detectedEntities.Any(e => argsToUse.EntityIds.Contains(e.LogId)) && argsToUse.EntityIds.Contains(entry.Source.LogId))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }

        private static void HandleEntityDeath(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if (argsToUse.EntityIds.Contains(entry.Target.LogId) && entry.Effect.EffectId == _7_0LogParsing.DeathCombatId)
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }

        private static void HandleEntityHP(ParsedLogEntry entry, Phase phase, bool starting)
        {
            if (_hpPhasesTriggered.TryGetValue(phase.Id, out var val) && val)
            {
                return;
            }
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if (((entry.TargetInfo.CurrentHP / (double)entry.TargetInfo.MaxHP) * 100d <= argsToUse.HPPercentage && argsToUse.EntityIds.Contains(entry.Target.LogId)) ||
                ((entry.SourceInfo.CurrentHP / (double)entry.SourceInfo.MaxHP) * 100d <= argsToUse.HPPercentage) && argsToUse.EntityIds.Contains(entry.Source.LogId))
            {
                if (starting)
                {
                    StartPhase(entry, phase);
                    _hpPhasesTriggered[phase.Id] = true;
                }
                else
                {
                    StopPhase(entry, phase);
                }
            }
        }

        private static void HandleAbilityUsage(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var activaePhaseOfSameType = ActivePhases.FirstOrDefault(p => p.SourcePhase.Id == phase.Id && p.PhaseEnd == DateTime.MinValue);
            if (activaePhaseOfSameType != null && starting)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.AbilityIds.Contains(entry.AbilityId.ToString()) || argsToUse.AbilityIds.Contains(entry.Ability)) && entry.Effect.EffectId == _7_0LogParsing.AbilityActivateId && (argsToUse.EntityIds.Contains(entry.Source.LogId) || argsToUse.EntityIds.Count == 0))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void HandleAbilityCancel(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.AbilityIds.Contains(entry.AbilityId.ToString()) || argsToUse.AbilityIds.Contains(entry.Ability)) && entry.Effect.EffectId == _7_0LogParsing.AbilityCancelId && (argsToUse.EntityIds.Contains(entry.Source.LogId) || argsToUse.EntityIds.Count == 0))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void HandleEffectGain(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var activaePhaseOfSameType = ActivePhases.FirstOrDefault(p => p.SourcePhase.Id == phase.Id && p.PhaseEnd == DateTime.MinValue);
            if (activaePhaseOfSameType != null && starting)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.EffectIds.Contains(entry.Effect.EffectName) || argsToUse.EffectIds.Contains(entry.Effect.EffectId.ToString())) && entry.Effect.EffectType == EffectType.Apply && (argsToUse.EntityIds.Contains(entry.Target.LogId) || argsToUse.EntityIds.Count == 0))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }

        private static void HandleEffectLoss(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var activePhase = ActivePhases.FirstOrDefault(p => p.PhaseEnd == DateTime.MinValue);
            if (activePhase != null && starting && activePhase.SourcePhase.EndTrigger != PhaseTrigger.Default)
                return;
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.EffectIds.Contains(entry.Effect.EffectName) || argsToUse.EffectIds.Contains(entry.Effect.EffectId.ToString())) && entry.Effect.EffectType == EffectType.Remove && (argsToUse.EntityIds.Contains(entry.Target.LogId) || argsToUse.EntityIds.Count == 0))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void HandleDuration(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var duration = 0d;
            var activePhase = ActivePhases.FirstOrDefault(p => p.SourcePhase.Name == phase.Name && p.PhaseEnd == DateTime.MinValue);
            if (starting)
            {
                if (activePhase == null || activePhase.SourcePhase.Id != phase.Id)
                    duration = (entry.TimeStamp - _combatStartTime).TotalSeconds;
            }
            if (!starting)
            {
                if (activePhase != null)
                {
                    duration = (entry.TimeStamp - activePhase.PhaseStart).TotalSeconds;
                }
            }
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if (argsToUse.CombatDuration < duration)
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void StartPhase(ParsedLogEntry entry, Phase phase)
        {
            var phaseInstance = new PhaseInstance()
            {
                Id = Guid.NewGuid(),
                PhaseStart = entry.TimeStamp,
                SourcePhase = phase
            };
            if (ActivePhases.Any())
            {
                var currentPhase = ActivePhases.Last();
                if (currentPhase.PhaseEnd == DateTime.MinValue)
                {
                    currentPhase.PhaseEnd = entry.TimeStamp;
                }
            }

            ActivePhases.Add(phaseInstance);
        }
        private static void StopPhase(ParsedLogEntry entry, Phase phase)
        {
            var currentPhase = ActivePhases.FirstOrDefault(p => p.SourcePhase.Name == phase.Name && p.PhaseEnd == DateTime.MinValue);
            if (currentPhase == null)
            {
                return;
            }
            currentPhase.PhaseEnd = entry.TimeStamp;
        }

        internal static void TogglePhaseInstance(PhaseInstance instance)
        {
            if (SelectedPhases.Any(p => p.PhaseStart == instance.PhaseStart && p.PhaseEnd == instance.PhaseEnd))
            {
                SelectedPhases.Remove(instance);
            }
            else
            {
                SelectedPhases.Add(instance);
            }
            SetSelectedPhaseDuration();
            SelectedPhasesUpdated.InvokeSafely(SelectedPhases);
        }
    }
}
