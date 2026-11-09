using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Challenge
{
    public class ChallengeUpdater
    {
        private ObservableCollection<ChallengeInstanceViewModel> _challenges;
        private List<DataStructures.Challenge> _activeChallenges = new List<DataStructures.Challenge>();
        private ConcurrentDictionary<Guid,DataStructures.Challenge> _allChallenges = new ConcurrentDictionary<Guid, DataStructures.Challenge>();
        private string _currentBossName;
        private int _replayGeneration;
        private readonly object _challengeStateLock = new();
        private readonly Dictionary<Guid, DataStructures.Challenge> _liveActiveChallenges = new();
        private int _liveGeneration;
        private DateTime _liveCombatStart;
        private DateTime _displayedCombatStart;
        private bool _displayedUsesLiveShell = true;
        private bool _showingLiveChallenges = true;
        private EncounterInfo _currentEncounter;
        private double _currentScale = 1;
        public ChallengeUpdater()
        {
            CombatLogStreamer.CombatStarted += ResetChallenges;
            CombatLogStreamer.HistoricalLogsStarted += ResetChallenges;
            CombatLogStreamer.NewLineStreamed += HandleLiveLog;
            CombatLogStreamer.CombatUpdated += TrackLiveCombat;

            EncounterTimerTrigger.BossCombatDetected += SetBossInfo;
            CombatLogStateBuilder.AreaEntered += EncounterChanged;
            RefreshChallenges();
        }

        private void UpdateChallengesWithPhases(IReadOnlyList<PhaseInstance> phases)
        {
            if (phases.Count == 0)
                _challenges.ForEach(c => c.UpdatePhase(null));
            foreach (var phaseChallenge in _challenges.Where(c => c.Type == ChallengeType.MetricDuringPhase))
            {
                phaseChallenge.UpdatePhase(
                    new ConcurrentDictionary<Guid, PhaseInstance>(
                        phaseChallenge.SourceChallenge.PhaseId == Guid.Empty
                            ? []
                            : phases
                                .Where(p => p.SourcePhase.Id == phaseChallenge.SourceChallenge.PhaseId)
                                .Select(p => new KeyValuePair<Guid, PhaseInstance>(p.Id, p))
                    )
                );
            }
        }

        public void UpdateScale(double scale)
        {
            _currentScale = scale;
        }
        private void EncounterChanged(EncounterInfo obj)
        {
            _currentEncounter = obj;
        }

        private void SetBossInfo(string encounterName, string bossName, string difficulty)
        {
            lock (_challengeStateLock) _currentBossName = bossName;
        }

        private void TrackLiveCombat(CombatStatusUpdate update)
        {
            if (update.CombatStartTime == DateTime.MinValue)
                return;
            lock (_challengeStateLock)
            {
                // An initial historical read can hand an already-running fight to
                // the live tail without another Start notification.
                if (_liveCombatStart == DateTime.MinValue || update.Type == UpdateType.Start)
                    _liveCombatStart = update.CombatStartTime;
                if (_displayedUsesLiveShell && _displayedCombatStart == _liveCombatStart)
                    _showingLiveChallenges = true;
            }
        }

        public void RefreshChallenges()
        {
            // build your new dictionary outside the lock
            var newDict = new ConcurrentDictionary<Guid, DataStructures.Challenge>(
                DefaultChallengeManager.GetAllDefaults()
                    .SelectMany(set => set.Challenges)
                    .Where(ch => ch.IsEnabled)
                    .Select(ch => new KeyValuePair<Guid, DataStructures.Challenge>(ch.Id, ch))
            );


                _allChallenges = newDict;
            
        }
        /// <summary>
        /// Called for **live parsing** (one log at a time).  
        /// Internally it finds any brand‐new challenges in that single log and immediately marshals them to the UI.
        /// </summary>
        public void HandleLiveLog(ParsedLogEntry log)
        {
            var phaseIds = PhaseManager.GetLivePhaseIds();
            IReadOnlyList<DataStructures.Challenge> newMatches;
            int liveGeneration;
            lock (_challengeStateLock)
            {
                liveGeneration = _liveGeneration;
                newMatches = FindNewChallengesFromLog(log, phaseIds);
            }
            if (!newMatches.Any()) return;
            // Never hold the state lock while waiting for the UI thread. A replay
            // may publish there while the parser is activating a live challenge.
            Dispatcher.UIThread.Invoke(() =>
            {
                lock (_challengeStateLock)
                {
                    if (liveGeneration != _liveGeneration || !_showingLiveChallenges)
                        return;
                    foreach (var challenge in newMatches)
                    {
                        if (_activeChallenges.Any(active => active.Id == challenge.Id))
                            continue;
                        _activeChallenges.Add(challenge);
                        _challenges.Add(new ChallengeInstanceViewModel(challenge) { Scale = _currentScale });
                    }
                }
            });
        }
        /// <summary>
        /// Called when the user “selects” an existing Combat.  
        /// This will scan _all_ logs in that Combat, collect every newly‐activated challenge,
        /// then do exactly one Dispatch → UI update at the end.
        /// </summary>
        public void HandleReplayCombat(Combat replayedCombat)
        {
            var generation = BeginReplay(replayedCombat);
            // Phases already belong to this selection. A later selection must not
            // change which phase challenges this worker activates.
            var phases = PhaseManager.GetDisplayedPhaseSnapshot().ToArray();
            var definitions = _allChallenges.Values.ToArray();
            _ = ReplayCombat(replayedCombat, definitions, phases, generation);
        }

        private int BeginReplay(Combat combat)
        {
            lock (_challengeStateLock)
            {
                _displayedCombatStart = combat.StartTime;
                _displayedUsesLiveShell = ReferenceEquals(combat, CombatIdentifier.CurrentCombat);
                _showingLiveChallenges = IsLiveCombat(combat);
                return Interlocked.Increment(ref _replayGeneration);
            }
        }

        private bool IsLiveCombat(Combat combat) => _liveCombatStart != DateTime.MinValue &&
            combat.StartTime == _liveCombatStart && ReferenceEquals(combat, CombatIdentifier.CurrentCombat);

        private async Task ReplayCombat(Combat replayedCombat, DataStructures.Challenge[] definitions,
            PhaseInstance[] phases, int generation)
        {
            using var replayTiming = LogLoadDiagnostics.Measure("Challenge replay");
            try
            {
                var activated = await Task.Run(() =>
                {
                    using var scanTiming = LogLoadDiagnostics.Measure("Challenge replay scan");
                    return FindReplayChallenges(replayedCombat.AllLogs.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value),
                        definitions, replayedCombat.EncounterBossDifficultyParts.Item1, phases,
                        () => generation == Volatile.Read(ref _replayGeneration));
                });
                if (activated == null)
                    return;
                await Dispatcher.UIThread.InvokeAsync(() => PublishReplay(replayedCombat, activated, phases, generation));
            }
            catch (Exception error)
            {
                Logging.LogError("Failed to replay challenges: " + error.Message);
            }
        }

        private static List<DataStructures.Challenge>? FindReplayChallenges(IEnumerable<ParsedLogEntry> logs,
            IEnumerable<DataStructures.Challenge> definitions, string bossName,
            IEnumerable<PhaseInstance> phases, Func<bool> isCurrent)
        {
            if (!isCurrent())
                return null;
            var phaseIds = phases.Select(phase => phase.SourcePhase.Id).ToHashSet();
            var candidates = new List<(DataStructures.Challenge Challenge, bool PhaseMatch)>();
            foreach (var challenge in definitions)
            {
                var phaseMatch = challenge.ChallengeType == ChallengeType.MetricDuringPhase && phaseIds.Contains(challenge.PhaseId);
                if (bossName == challenge.Source.Split('|')[1] || phaseMatch)
                    candidates.Add((challenge, phaseMatch));
            }

            var activated = new List<DataStructures.Challenge>();
            if (candidates.Count == 0)
                return activated;
            foreach (var log in logs)
            {
                if (!isCurrent())
                    return null;
                for (var index = 0; index < candidates.Count;)
                {
                    var candidate = candidates[index];
                    if (candidate.PhaseMatch || IsLogForChallenge(log, candidate.Challenge))
                    {
                        activated.Add(candidate.Challenge);
                        candidates.RemoveAt(index);
                    }
                    else
                        index++;
                }
                if (candidates.Count == 0)
                    break;
            }
            return activated;
        }

        // Called on the UI thread, so collection replacement and metric requests
        // cannot interleave with another selection's publication.
        private bool PublishReplay(Combat combat, List<DataStructures.Challenge> activated,
            PhaseInstance[] replayPhases, int generation)
        {
            if (generation != Volatile.Read(ref _replayGeneration))
                return false;
            // Snapshot phases before taking our own lock, avoiding lock inversion
            // with the phase accumulator used by the parser thread.
            var livePhases = PhaseManager.GetLivePhaseSnapshot();
            lock (_challengeStateLock)
            {
                var merged = MergeReplayActivations(combat, activated, generation);
                if (merged == null)
                    return false;
                ClearChallenges();
                _activeChallenges.AddRange(merged);
                foreach (var challenge in merged)
                    _challenges.Add(new ChallengeInstanceViewModel(challenge) { Scale = _currentScale });
                UpdateChallengeMetrics(combat, _showingLiveChallenges ? livePhases : replayPhases);
                return true;
            }
        }

        private List<DataStructures.Challenge>? MergeReplayActivations(Combat combat,
            List<DataStructures.Challenge> activated, int generation)
        {
            lock (_challengeStateLock)
            {
                if (generation != Volatile.Read(ref _replayGeneration))
                    return null;
                _showingLiveChallenges = IsLiveCombat(combat);
                // A live one-shot event may have arrived after the replay's log
                // snapshot (and even before that line reaches Combat.AllLogs).
                // Preserve it for this fight without leaking it into history.
                return _showingLiveChallenges
                    ? activated.Concat(_liveActiveChallenges.Values).DistinctBy(challenge => challenge.Id).ToList()
                    : activated.ToList();
            }
        }
        /// <summary>
        /// Scans one ParsedLogEntry against all known challenges.
        /// If a match is found and it wasn’t already active in this live fight,
        /// it adds it here and returns that challenge.
        /// </summary>
        private IReadOnlyList<DataStructures.Challenge> FindNewChallengesFromLog(ParsedLogEntry log,
            IReadOnlySet<Guid> phaseIds)
        {
            var newlyActivated = new List<DataStructures.Challenge>();

            foreach (var kvp in _allChallenges)
            {
                var challenge = kvp.Value;

                bool logMatch   = IsLogForChallenge(log, challenge);
                bool bossMatch  = (_currentBossName == challenge.Source.Split('|')[1]);
                bool phaseMatch = (challenge.ChallengeType == ChallengeType.MetricDuringPhase)
                                  && phaseIds.Contains(challenge.PhaseId);

                // require (logMatch AND bossMatch) OR phaseMatch
                if ((logMatch && bossMatch) || phaseMatch)
                {
                    // Retain each live activation independently of the selected display.
                    if (_liveActiveChallenges.TryAdd(kvp.Key, challenge))
                    {
                        newlyActivated.Add(challenge);
                    }
                }
            }

            return newlyActivated;
        }
        private static bool IsLogForChallenge(ParsedLogEntry log, DataStructures.Challenge challenge)
        {
            switch (challenge.ChallengeType)
            {
                case ChallengeType.DamageOut:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId.ToString() == challenge.Value || string.IsNullOrEmpty(challenge.Value)) && (log.Target.Name == challenge.ChallengeTarget || log.Target.LogId.ToString() == challenge.ChallengeTarget || string.IsNullOrEmpty(challenge.ChallengeTarget));
                    }
                case ChallengeType.DamageIn:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId.ToString() == challenge.Value || string.IsNullOrEmpty(challenge.Value)) && (log.Source.Name == challenge.ChallengeSource || log.Source.LogId.ToString() == challenge.ChallengeSource || string.IsNullOrEmpty(challenge.ChallengeSource));
                    }
                case ChallengeType.AbilityCount:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId.ToString() == challenge.Value);
                    }
                case ChallengeType.InterruptCount:
                    {
                        return log.AbilityId == _7_0LogParsing.InterruptCombatId;
                    }
                case ChallengeType.EffectStacks:
                    {
                        return log.Effect.EffectName == challenge.Value || log.Effect.EffectId.ToString() == challenge.Value;
                    }
                default:
                    {
                        return false;
                    }
            }
        }
        internal void SetCollection(ObservableCollection<ChallengeInstanceViewModel> activeChallengeInstances)
        {
            _challenges = activeChallengeInstances;
        }

        private void ResetChallenges()
        {
            var generation = ResetLiveChallengeState();
            Dispatcher.UIThread.Invoke(() =>
            {
                lock (_challengeStateLock)
                {
                    if (generation == Volatile.Read(ref _replayGeneration))
                        ClearChallenges();
                }
            });
        }

        private int ResetLiveChallengeState()
        {
            lock (_challengeStateLock)
            {
                _liveGeneration++;
                _liveActiveChallenges.Clear();
                _liveCombatStart = DateTime.MinValue;
                _displayedCombatStart = DateTime.MinValue;
                _displayedUsesLiveShell = true;
                _showingLiveChallenges = true;
                return Interlocked.Increment(ref _replayGeneration);
            }
        }

        private void ClearChallenges()
        {
            _activeChallenges.Clear();
            _challenges.ForEach(c => c.Reset());
            _challenges.Clear();
        }

        public void UpdateCombats(Combat obj)
        {
            var phases = PhaseManager.GetLivePhaseSnapshot();
            lock (_challengeStateLock)
            {
                if (!_showingLiveChallenges || !IsLiveCombat(obj))
                    return;
                // Also include raw events observed during the historical-to-live
                // handoff before the first live batch established its start time.
                foreach (var challenge in _liveActiveChallenges.Values)
                {
                    if (_activeChallenges.Any(active => active.Id == challenge.Id))
                        continue;
                    _activeChallenges.Add(challenge);
                    _challenges.Add(new ChallengeInstanceViewModel(challenge) { Scale = _currentScale });
                }
                UpdateChallengeMetrics(obj, phases);
            }
        }

        private void UpdateChallengeMetrics(Combat obj, IReadOnlyList<PhaseInstance> phases)
        {
            UpdateChallengesWithPhases(phases);
            var activeChallenges = _activeChallenges.ToList();
            foreach (var challenge in activeChallenges)
            {
                var activeChallenge = _challenges.FirstOrDefault(c => c.SourceChallengeId == challenge.Id);
                if (activeChallenge != null)
                    activeChallenge.UpdateMetrics(obj, challenge);
            }
        }
    }
}
