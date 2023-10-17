using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Phases;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public Phase SourcePhase { get; set; }
        public DateTime PhaseStart { get; set; }
        public DateTime PhaseEnd { get; set; }

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
    public class PhaseManager
    {
        // Define a dictionary to map PhaseTrigger to delegate
        private readonly Dictionary<PhaseTrigger, Action<ParsedLogEntry, Phase, bool>> triggerHandlers = new Dictionary<PhaseTrigger, Action<ParsedLogEntry, Phase, bool>>
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
        private static List<Entity> _detectedEntities = new List<Entity>();

        private string _currentBossName;
        private static DateTime _combatStartTime;
        private static bool _combatStartToggled;
        private static IEnumerable<Phase> _loadedPhases { get; set; }
        public static List<PhaseInstance> ActivePhases { get; set; } = new List<PhaseInstance>();
        public static List<PhaseInstance> SelectedPhases { get; set; } = new List<PhaseInstance>();

        public static event Action<List<PhaseInstance>> PhaseInstancesUpdated = delegate { };
        public static event Action<List<PhaseInstance>> SelectedPhasesUpdated = delegate { };
        public static event Action<Phase> PhaseStarted = delegate { };
        public static event Action<Phase> PhaseEnded = delegate { };

        public PhaseManager()
        {
            //need to load phases from a .json file that can be edited by the user
            _loadedPhases = DefaultPhaseManager.GetExisitingPhases();
            //need to reset the phases when combat starts
            CombatLogStreamer.CombatUpdated += UpdatePhases;
            CombatSelectionMonitor.NewCombatSelected += UpdateForSelectedCombat;

            PhaseListViewModel.PhasesUpdated += () =>
            {
                _loadedPhases = DefaultPhaseManager.GetExisitingPhases();
            };

            EncounterTimerTrigger.EncounterDetected += SetBossInfo;
        }
        private void SetBossInfo(string encounterName, string bossName, string difficulty)
        {
            _currentBossName = bossName;
        }
        private void UpdateForSelectedCombat(Combat combat)
        {
            _loadedPhases = DefaultPhaseManager.GetExisitingPhases();
            ResetPhases();
            _currentBossName = combat.EncounterBossDifficultyParts.Item1;
            foreach (var line in combat.AllLogs)
            {
                HandleNewLine(line);
            }
            if (!ActivePhases.Any())
                PhaseInstancesUpdated(ActivePhases);
        }
        private void UpdatePhases(CombatStatusUpdate update)
        {
            if (update.Type == UpdateType.Start)
            {
                ResetPhases();
                _combatStartTime = update.CombatStartTime;
            }
            if (update.Type == UpdateType.Update)
            {
                foreach (var line in update.Logs)
                {
                    HandleNewLine(line);
                }
            }
        }
        private void UpdateActiveEntities(ParsedLogEntry entry)
        {
            if (!_detectedEntities.Any(x => x.LogId == entry.Source.LogId) && entry.Effect.EffectType == EffectType.TargetChanged)
            {
                _detectedEntities.Add(entry.Source);
            }

            if (!_detectedEntities.Any(x => x.LogId == entry.Target.LogId) && entry.Effect.EffectType == EffectType.TargetChanged)
            {
                _detectedEntities.Add(entry.Target);
            }
            if(entry.Effect.EffectId == _7_0LogParsing.DeathCombatId)
            {
                _detectedEntities.Remove(_detectedEntities.FirstOrDefault(x => x.LogId == entry.Target.LogId));
            }
        }

        private void ResetPhases()
        {
            _combatStartToggled = false;
            _detectedEntities.Clear();
            ActivePhases.Clear();
            SelectedPhases.Clear();
            SelectedPhasesUpdated(SelectedPhases);
        }

        private void HandleNewLine(ParsedLogEntry entry)
        {
            foreach (var phase in _loadedPhases.Where(p => p.SourcePt2 ==_currentBossName))
            {
                if (triggerHandlers.ContainsKey(phase.StartTrigger))
                {
                    triggerHandlers[phase.StartTrigger](entry, phase, true);
                }

                if (triggerHandlers.ContainsKey(phase.EndTrigger))
                {
                    triggerHandlers[phase.EndTrigger](entry, phase, false);
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
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if (!_detectedEntities.Any(e => argsToUse.EntityIds.Contains(e.LogId)) && (argsToUse.EntityIds.Contains(entry.Source.LogId) || argsToUse.EntityIds.Contains(entry.Target.LogId)) && entry.Effect.EffectType == EffectType.TargetChanged)
            {
                if(starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }

        private static void HandleEntityDeath(ParsedLogEntry entry, Phase phase, bool starting)
        {
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
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if (((entry.TargetInfo.CurrentHP / entry.TargetInfo.MaxHP) * 100 <= argsToUse.HPPercentage && argsToUse.EntityIds.Contains(entry.Target.LogId)) ||
                ((entry.SourceInfo.CurrentHP / entry.SourceInfo.MaxHP) * 100 <= argsToUse.HPPercentage) && argsToUse.EntityIds.Contains(entry.Target.LogId))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }

        private static void HandleAbilityUsage(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.AbilityIds.Contains(entry.AbilityId) || argsToUse.AbilityIds.Contains(entry.Ability)) && entry.Effect.EffectId == _7_0LogParsing.AbilityActivateId && (argsToUse.EntityIds.Contains(entry.Source.LogId)||!argsToUse.EntityIds.Any()))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void HandleAbilityCancel(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.AbilityIds.Contains(entry.AbilityId) || argsToUse.AbilityIds.Contains(entry.Ability)) && entry.Effect.EffectId == _7_0LogParsing.AbilityCancelId && (argsToUse.EntityIds.Contains(entry.Source.LogId) || !argsToUse.EntityIds.Any()))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private static void HandleEffectGain(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.EffectIds.Contains(entry.Effect.EffectName) || argsToUse.EffectIds.Contains(entry.Effect.EffectId)) && entry.Effect.EffectType == EffectType.Apply && (argsToUse.EntityIds.Contains(entry.Target.LogId) || !argsToUse.EntityIds.Any()))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }

        private static void HandleEffectLoss(ParsedLogEntry entry, Phase phase, bool starting)
        {
            var argsToUse = starting ? phase.StartArgs : phase.EndArgs;
            if ((argsToUse.EffectIds.Contains(entry.Effect.EffectName) || argsToUse.EffectIds.Contains(entry.Effect.EffectId)) && entry.Effect.EffectType == EffectType.Remove && (argsToUse.EntityIds.Contains(entry.Target.LogId) || !argsToUse.EntityIds.Any()))
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
            if (starting)
            {
                duration = (entry.TimeStamp - _combatStartTime).TotalSeconds;
            }
            else
            {
                var activePhase = ActivePhases.FirstOrDefault(p => p.SourcePhase.Name == phase.Name && p.PhaseEnd == DateTime.MinValue);
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
            PhaseStarted(phase);
            //PhaseInstancesUpdated(ActivePhases);
        }
        private static void StopPhase(ParsedLogEntry entry, Phase phase)
        {
            var currentPhase = ActivePhases.FirstOrDefault(p => p.SourcePhase.Name == phase.Name && p.PhaseEnd == DateTime.MinValue);
            if(currentPhase == null)
            {
                return;
            }
            currentPhase.PhaseEnd = entry.TimeStamp;
            PhaseEnded(phase);
            //PhaseInstancesUpdated(ActivePhases);
        }

        internal static void TogglePhaseInstance(PhaseInstance instance)
        {
            if(SelectedPhases.Any(p=>p.PhaseStart == instance.PhaseStart && p.PhaseEnd == instance.PhaseEnd))
            {
                SelectedPhases.Remove(instance);
            }
            else
            {
                SelectedPhases.Add(instance);
            }
            SelectedPhasesUpdated(SelectedPhases);
        }
    }
}
