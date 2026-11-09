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
using System.Collections.Frozen;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
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
        private static readonly object _selectionLock = new();
        private static readonly PhaseAccumulator _live = new();
        private static IEnumerable<Phase> _loadedPhases { get; set; } = Array.Empty<Phase>();
        private static string _currentBossName;
        private static long _displayGeneration;
        private static bool _showingHistorical;
        private static Combat _displayedCombat;
        private static DateTime _displayedCombatEnd;
        private static double phaseDuration;
        private static IReadOnlySet<Guid> _livePhaseIds = Array.Empty<Guid>().ToFrozenSet();

        // Display state is independent from the accumulator consuming live records.
        public static List<PhaseInstance> ActivePhases { get; set; } = new();
        public static List<PhaseInstance> SelectedPhases { get; set; } = new();
        public static double PhaseDuration => phaseDuration * 1000f;
        public static event Action<List<PhaseInstance>> PhaseInstancesUpdated = delegate { };
        public static event Action<List<PhaseInstance>> SelectedPhasesUpdated = delegate { };

        internal sealed record PreparedSelection(List<PhaseInstance> Raw, List<PhaseInstance> Clean);
        private sealed record CachedSelection(IEnumerable<Phase> Definitions, int LogCount, DateTime End, PreparedSelection Phases);
        private static readonly ConditionalWeakTable<Combat, CachedSelection> _preparedSelections = new();

        public static void Init()
        {
            _loadedPhases = DefaultPhaseManager.GetExisitingPhases().ToArray();
            CombatLogStreamer.CombatUpdated += UpdatePhases;
            CombatLogStreamer.HistoricalLogsStarted += ResetPhases;
            // Historical reconstruction is prepared by the row-load worker before
            // CombatSelected is published. It must never block the UI or live state.
            PhaseListViewModel.PhasesUpdated += () =>
            {
                var definitions = DefaultPhaseManager.GetExisitingPhases().ToArray();
                lock (_selectionLock) _loadedPhases = definitions;
            };
            EncounterTimerTrigger.BossCombatDetected += SetBossInfo;
        }

        private static void SetBossInfo(string encounterName, string bossName, string difficulty)
        {
            lock (_selectionLock) _currentBossName = bossName;
        }

        /// <summary>A detached copy of raw live phases; open ends remain open.</summary>
        public static List<PhaseInstance> GetLivePhaseSnapshot()
        {
            lock (_selectionLock) return CopyPhases(_live.ActivePhases);
        }

        /// <summary>Immutable definition IDs, refreshed once per live batch, safe for per-line matching.</summary>
        public static IReadOnlySet<Guid> GetLivePhaseIds() => Volatile.Read(ref _livePhaseIds);

        /// <summary>A detached copy of phases belonging to the currently displayed selection.</summary>
        public static List<PhaseInstance> GetDisplayedPhaseSnapshot()
        {
            lock (_selectionLock) return CopyPhases(ActivePhases);
        }

        public static bool IsDisplayingCombat(Combat combat)
        {
            lock (_selectionLock)
                return _showingHistorical
                    ? ReferenceEquals(_displayedCombat, combat)
                    : _live._combatStartTime == combat.StartTime;
        }

        // Called by the existing background combat-load worker. Definitions and
        // trigger state belong to this calculation, never to the live accumulator.
        internal static PreparedSelection? PrepareSelection(Combat combat, Func<bool>? isCurrent = null)
        {
            using var timing = LogLoadDiagnostics.Measure("Selected combat phase reconstruction");
            if (isCurrent != null && !isCurrent()) return null;
            IEnumerable<Phase> definitions;
            lock (_selectionLock)
            {
                definitions = _loadedPhases;
                if (GetCachedSelection(combat, definitions) is { } cached) return cached;
            }
            var prepared = Reconstruct(combat.EncounterBossDifficultyParts.Item1, combat.StartTime, combat.EndTime,
                combat.AllLogs.OrderBy(pair => pair.Key).Select(pair => pair.Value), definitions, isCurrent);
            if (prepared == null) return null;
            lock (_selectionLock)
            {
                _preparedSelections.Remove(combat);
                _preparedSelections.Add(combat, new CachedSelection(definitions, combat.AllLogs.Count, combat.EndTime, prepared));
            }
            return new PreparedSelection(CopyPhases(prepared.Raw), CopyPhases(prepared.Clean));
        }

        // Cached phases and fights with no matching definitions can be published
        // together with the combat without waiting for a second worker pass.
        internal static PreparedSelection? GetPreparedSelectionIfReady(Combat combat)
        {
            lock (_selectionLock)
            {
                if (GetCachedSelection(combat, _loadedPhases) is { } cached) return cached;
                return _loadedPhases.Any(phase => phase.SourcePt2 == combat.EncounterBossDifficultyParts.Item1)
                    ? null : new PreparedSelection(new(), new());
            }
        }

        private static PreparedSelection? GetCachedSelection(Combat combat, IEnumerable<Phase> definitions)
        {
            if (_preparedSelections.TryGetValue(combat, out var cached) && ReferenceEquals(cached.Definitions, definitions) &&
                cached.LogCount == combat.AllLogs.Count && cached.End == combat.EndTime)
                return new PreparedSelection(CopyPhases(cached.Phases.Raw), CopyPhases(cached.Phases.Clean));
            return null;
        }

        internal static IEnumerable<Phase> CaptureDefinitions()
        {
            lock (_selectionLock) return _loadedPhases;
        }

        internal static PreparedSelection? Reconstruct(string boss, DateTime start, DateTime end,
            IEnumerable<ParsedLogEntry> orderedLogs, IEnumerable<Phase> definitions, Func<bool>? isCurrent)
        {
            var state = new PhaseAccumulator
            {
                _loadedPhases = definitions.Where(phase => phase.SourcePt2 == boss).ToArray(),
                _currentBossName = boss,
                _combatStartTime = start,
                _combatStopTime = end
            };
            if (state._loadedPhases.Any())
            foreach (var line in orderedLogs)
            {
                if (isCurrent != null && !isCurrent()) return null;
                state.HandleNewLine(line);
            }
            return new PreparedSelection(state.ActivePhases, state.CleanPhases());
        }

        internal static void PublishSelection(Combat combat, PreparedSelection? prepared, bool isCurrentCombat)
        {
            long generation;
            PreparedSelection snapshot;
            lock (_selectionLock)
            {
                generation = ++_displayGeneration;
                _showingHistorical = !isCurrentCombat;
                _displayedCombat = combat;
                _displayedCombatEnd = combat.EndTime;
                snapshot = isCurrentCombat
                    ? new PreparedSelection(CopyPhases(_live.ActivePhases), _live.CleanPhases())
                    : prepared ?? new PreparedSelection(new(), new());
            }
            PublishDisplay(snapshot, generation, clearSelection: true);
        }

        // Compatibility entry point for callers outside normal row selection.
        // The normal path supplies its already prepared snapshot before UI publication.
        public static async void UpdateForSelectedCombat(Combat combat)
        {
            long generation;
            lock (_selectionLock) generation = ++_displayGeneration;
            try
            {
                var snapshot = await Task.Run(() => PrepareSelection(combat,
                    () => generation == Volatile.Read(ref _displayGeneration)));
                if (snapshot == null) return;
                DispatchDisplay(() =>
                {
                    lock (_selectionLock)
                    {
                        if (generation != _displayGeneration) return;
                        _showingHistorical = true;
                        _displayedCombat = combat;
                        _displayedCombatEnd = combat.EndTime;
                    }
                    PublishDisplay(snapshot, generation, clearSelection: true);
                });
            }
            catch (Exception error) { Logging.LogError("Failed to load selected combat phases: " + error.Message); }
        }

        private static void UpdatePhases(CombatStatusUpdate update)
        {
            PreparedSelection snapshot = null;
            long generation;
            bool clearSelection = update.Type == UpdateType.Start;
            lock (_selectionLock)
            {
                if (clearSelection)
                {
                    _live.ResetPhases();
                    _live._combatStartTime = update.CombatStartTime;
                    _live._combatStopTime = update.CombatStartTime;
                    _showingHistorical = false;
                    _displayedCombat = null;
                    ++_displayGeneration;
                }
                _live._loadedPhases = _loadedPhases;
                _live._currentBossName = _currentBossName;
                if (update.Logs != null)
                    foreach (var line in update.Logs.OrderBy(line => line.LogLineNumber))
                    {
                        if (line.TimeStamp > _live._combatStopTime) _live._combatStopTime = line.TimeStamp;
                        _live.HandleNewLine(line);
                    }
                _livePhaseIds = _live.ActivePhases.Select(phase => phase.SourcePhase.Id).ToFrozenSet();
                generation = _displayGeneration;
                if (!_showingHistorical)
                {
                    _displayedCombatEnd = _live._combatStopTime;
                    snapshot = new PreparedSelection(CopyPhases(_live.ActivePhases), _live.CleanPhases());
                }
            }
            if (snapshot != null) PublishDisplay(snapshot, generation, clearSelection);
        }

        private static void PublishDisplay(PreparedSelection snapshot, long generation, bool clearSelection)
        {
            DispatchDisplay(() =>
            {
                lock (_selectionLock)
                {
                    if (generation != _displayGeneration) return;
                    ActivePhases = snapshot.Raw;
                    if (clearSelection)
                    {
                        SelectedPhases.Clear();
                        phaseDuration = 0;
                    }
                }
                PhaseInstancesUpdated.InvokeSafely(snapshot.Clean);
            });
        }

        private static void DispatchDisplay(Action publish)
        {
            if (Avalonia.Application.Current == null || Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) publish();
            else Avalonia.Threading.Dispatcher.UIThread.Invoke(publish);
        }

        private static List<PhaseInstance> CopyPhases(IEnumerable<PhaseInstance> phases) => phases.Select(phase => new PhaseInstance
        {
            Id = phase.Id, SourcePhase = phase.SourcePhase, PhaseStart = phase.PhaseStart,
            PhaseEnd = phase.PhaseEnd, ArtificallyEnded = phase.ArtificallyEnded
        }).ToList();

        private static void ResetPhases()
        {
            long generation;
            lock (_selectionLock)
            {
                _live.ResetPhases();
                _live._combatStartTime = _live._combatStopTime = DateTime.MinValue;
                _livePhaseIds = Array.Empty<Guid>().ToFrozenSet();
                _showingHistorical = false;
                _displayedCombat = null;
                _displayedCombatEnd = DateTime.MinValue;
                generation = ++_displayGeneration;
            }
            PublishDisplay(new PreparedSelection(new(), new()), generation, clearSelection: true);
        }

        public static void SetSelectedPhaseDuration()
        {
            lock (_selectionLock)
            {
                phaseDuration = SelectedPhases.Sum(phase =>
                {
                    var end = phase.PhaseEnd;
                    if (end == DateTime.MinValue)
                        end = _displayedCombatEnd != DateTime.MinValue
                            ? _displayedCombatEnd : CombatIdentifier.CurrentCombat?.EndTime ?? phase.PhaseStart;
                    return (end - phase.PhaseStart).TotalSeconds;
                });
            }
        }

        internal static void TogglePhaseInstance(PhaseInstance instance)
        {
            if (SelectedPhases.Any(phase => phase.PhaseStart == instance.PhaseStart && phase.PhaseEnd == instance.PhaseEnd))
                SelectedPhases.Remove(instance);
            else SelectedPhases.Add(instance);
            SetSelectedPhaseDuration();
            SelectedPhasesUpdated.InvokeSafely(SelectedPhases);
        }

        // This is the existing phase state machine with instance-owned state. One
        // instance consumes the live stream; each historical scan gets a fresh one.
        private sealed class PhaseAccumulator
        {
            private readonly Dictionary<PhaseTrigger, Action<ParsedLogEntry, Phase, bool>> triggerHandlers;
            private readonly HashSet<Entity> _detectedEntities = new();
            private readonly HashSet<(Guid, bool)> _combatStartToggled = new();
            private readonly Dictionary<Guid, bool> _hpPhasesTriggered = new();
            public string _currentBossName;
            public DateTime _combatStartTime;
            public DateTime _combatStopTime;
            public IEnumerable<Phase> _loadedPhases = Array.Empty<Phase>();
            public List<PhaseInstance> ActivePhases = new();
            public PhaseAccumulator()
            {
                triggerHandlers = new()
                {
                    { PhaseTrigger.CombatStart, HandleCombatStart },
                    { PhaseTrigger.EntitySpawn, HandleEntitySpawn },
                    { PhaseTrigger.EntityDeath, HandleEntityDeath },
                    { PhaseTrigger.EntityHP, HandleEntityHP },
                    { PhaseTrigger.AbilityUsage, HandleAbilityUsage },
                    { PhaseTrigger.AbilityCancel, HandleAbilityCancel },
                    { PhaseTrigger.EffectGain, HandleEffectGain },
                    { PhaseTrigger.EffectLoss, HandleEffectLoss },
                    { PhaseTrigger.CombatDuration, HandleDuration }
                };
            }
        public List<PhaseInstance> CleanPhases()
        {
            // 1) Sort (work on a local list; we will mutate PhaseStart/End on the instances themselves)
            var phases = ActivePhases
                .Select(p => new PhaseInstance
                {
                    Id = p.Id, SourcePhase = p.SourcePhase, PhaseStart = p.PhaseStart,
                    PhaseEnd = p.PhaseEnd, ArtificallyEnded = p.ArtificallyEnded
                })
                .OrderBy(p => p.PhaseStart)
                .ToList();

            if (phases.Count == 0)
                return phases;

            // Helper: treat MinValue as "unassigned"
            static bool EndUnassigned(PhaseInstance p) => p.PhaseEnd == DateTime.MinValue;

            // 2) Normalize ends (do not mutate neighbors)
            for (int i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                var nextStart = (i + 1 < phases.Count) ? phases[i + 1].PhaseStart : _combatStopTime;

                // If unassigned or invalid, clamp to nextStart (or combat stop time if last)
                if (EndUnassigned(p) || p.PhaseEnd < p.PhaseStart)
                {
                    p.PhaseEnd = nextStart;
                }

                // If for any reason nextStart is before this start (bad data), ensure non-negative duration
                if (p.PhaseEnd < p.PhaseStart)
                    p.PhaseEnd = p.PhaseStart;
            }

            // 3) Merge neighboring phases with same SourcePhase
            //    After step (2), phases are guaranteed End >= Start and no "missing ends".
            var merged = new List<PhaseInstance>(phases.Count);
            merged.Add(phases[0]);

            for (int i = 1; i < phases.Count; i++)
            {
                var cur = phases[i];
                var prev = merged[^1];

                if (prev.SourcePhase == cur.SourcePhase)
                {
                    // Merge: expand prev to include cur.
                    // Since ends were normalized, this also handles overlaps or adjacency cleanly.
                    if (cur.PhaseStart < prev.PhaseStart)
                        prev.PhaseStart = cur.PhaseStart;

                    if (cur.PhaseEnd > prev.PhaseEnd)
                        prev.PhaseEnd = cur.PhaseEnd;

                    // Optional: if you have other fields (name, metadata, etc.), decide how to combine them here.
                    continue;
                }

                merged.Add(cur);
            }

            // 4) Final safety: ensure monotonic & clamp any weirdness (optional but cheap)
            //    This ensures End never goes beyond combat stop time.
            foreach (var p in merged)
            {
                if (p.PhaseEnd > _combatStopTime)
                    p.PhaseEnd = _combatStopTime;

                if (p.PhaseEnd < p.PhaseStart)
                    p.PhaseEnd = p.PhaseStart;
            }

            return merged;
        }

        private void UpdateActiveEntities(ParsedLogEntry entry)
        {
            _detectedEntities.Add(entry.Source);
            if (entry.Effect.EffectId == _7_0LogParsing.DeathCombatId)
            {
                _detectedEntities.RemoveWhere(x => x.LogId == entry.Target.LogId);
            }
        }

        public void ResetPhases()
        {
            _combatStartToggled.Clear();
            _detectedEntities.Clear();
            ActivePhases.Clear();

            _hpPhasesTriggered.Clear();

        }

        public void HandleNewLine(ParsedLogEntry entry)
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
        private void HandleCombatStart(ParsedLogEntry entry, Phase phase, bool starting)
        {
            if (_combatStartToggled.Add((phase.Id, starting)))
            {
                if (starting)
                    StartPhase(entry, phase);
                else
                    StopPhase(entry, phase);
            }
        }
        private void HandleEntitySpawn(ParsedLogEntry entry, Phase phase, bool starting)
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

        private void HandleEntityDeath(ParsedLogEntry entry, Phase phase, bool starting)
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

        private void HandleEntityHP(ParsedLogEntry entry, Phase phase, bool starting)
        {
            if (starting && _hpPhasesTriggered.TryGetValue(phase.Id, out var val) && val)
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

        private void HandleAbilityUsage(ParsedLogEntry entry, Phase phase, bool starting)
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
        private void HandleAbilityCancel(ParsedLogEntry entry, Phase phase, bool starting)
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
        private void HandleEffectGain(ParsedLogEntry entry, Phase phase, bool starting)
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

        private void HandleEffectLoss(ParsedLogEntry entry, Phase phase, bool starting)
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
        private void HandleDuration(ParsedLogEntry entry, Phase phase, bool starting)
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
        private void StartPhase(ParsedLogEntry entry, Phase phase)
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
        private void StopPhase(ParsedLogEntry entry, Phase phase)
        {
            var currentPhase = ActivePhases.FirstOrDefault(p => p.SourcePhase.Name == phase.Name && p.PhaseEnd == DateTime.MinValue);
            if (currentPhase == null)
            {
                return;
            }
            currentPhase.PhaseEnd = entry.TimeStamp;
        }

        }
    }
}
