using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SWTORCombatParser.Model.Challenge
{
    public class ChallengeUpdater
    {
        private ObservableCollection<ChallengeInstanceViewModel> _challenges;
        private List<DataStructures.Challenge> _activeChallenges = new List<DataStructures.Challenge>();
        private ConcurrentDictionary<Guid,DataStructures.Challenge> _allChallenges = new ConcurrentDictionary<Guid, DataStructures.Challenge>();
        private string _currentBossName;
        private Combat _currentSelectedCombat;
        private EncounterInfo _currentEncounter;
        private double _currentScale = 1;
        public ChallengeUpdater()
        {
            CombatLogStreamer.CombatStarted += ResetChallenges;
            CombatLogStreamer.NewLineStreamed += HandleLiveLog;

            EncounterTimerTrigger.BossCombatDetected += SetBossInfo;
            CombatLogStateBuilder.AreaEntered += EncounterChanged;
            RefreshChallenges();
        }

        private void UpdateChallengesWithPhases()
        {
            if (PhaseManager.ActivePhases.Count == 0)
                _challenges.ForEach(c => c.UpdatePhase(null));
            foreach (var phaseChallenge in _challenges.Where(c => c.Type == ChallengeType.MetricDuringPhase))
            {
                phaseChallenge.UpdatePhase(
                    new ConcurrentDictionary<Guid, PhaseInstance>(
                        phaseChallenge.SourceChallenge.PhaseId == Guid.Empty
                            ? []
                            : PhaseManager.ActivePhases
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
            _currentBossName = bossName;
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
            // 1) Get all brand‐new matches out of this one line
            var newMatches = FindNewChallengesFromLog(log);
            if (!newMatches.Any()) return;

            // 2) Convert to ViewModels (using current scale)
            var newViewModels = newMatches
                .Select(ch => new ChallengeInstanceViewModel(ch) { Scale = _currentScale })
                .ToList();

            // 3) Immediately push them onto the UI thread
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var vm in newViewModels)
                    _challenges.Add(vm);
            });
        }
        /// <summary>
        /// Called when the user “selects” an existing Combat.  
        /// This will scan _all_ logs in that Combat, collect every newly‐activated challenge,
        /// then do exactly one Dispatch → UI update at the end.
        /// </summary>
        public void HandleReplayCombat(Combat replayedCombat)
        {
            Task.Run(() =>
            {
                // 1) Reset state
                _currentBossName = replayedCombat.EncounterBossDifficultyParts.Item1;
                _currentSelectedCombat = replayedCombat;
                ResetChallenges();

                // 2) Scan every log, accumulate new ViewModels in a local buffer
                var buffer = new List<ChallengeInstanceViewModel>();
                foreach (var logLine in replayedCombat.AllLogs.OrderBy(kvp=>kvp.Key))
                {
                    var newlyFound = FindNewChallengesFromLog(logLine.Value);
                    if (newlyFound.Count == 0) 
                        continue;

                    buffer.AddRange(
                        newlyFound.Select(ch => new ChallengeInstanceViewModel(ch)
                        {
                            Scale = _currentScale
                        })
                    );
                }

                // 3) One‐time UI dispatch: add all buffered VMs
                Dispatcher.UIThread.Invoke(() =>
                {
                    foreach (var vm in buffer)
                        _challenges.Add(vm);
                });

                // 4) Finally update metrics for each active challenge
                UpdateCombats(replayedCombat);
            });
        }
        /// <summary>
        /// Scans one ParsedLogEntry against all known challenges.
        /// If a match is found and it wasn’t already in _activeChallenges,
        /// it adds it here and returns that challenge.
        /// </summary>
        private IReadOnlyList<DataStructures.Challenge> FindNewChallengesFromLog(ParsedLogEntry log)
        {
            var newlyActivated = new List<DataStructures.Challenge>();

            // Take a snapshot of phases to avoid enumerating PhaseManager.ActivePhases each time
            var phaseSnapshot = PhaseManager.ActivePhases.ToList();

            foreach (var kvp in _allChallenges)
            {
                var challenge = kvp.Value;

                bool logMatch   = IsLogForChallenge(log, challenge);
                bool bossMatch  = (_currentBossName == challenge.Source.Split('|')[1]);
                bool phaseMatch = (challenge.ChallengeType == ChallengeType.MetricDuringPhase)
                                  && phaseSnapshot.Any(p => p.SourcePhase.Id == challenge.PhaseId);

                // require (logMatch AND bossMatch) OR phaseMatch
                if ((logMatch && bossMatch) || phaseMatch)
                {
                    // Only add if it's not already in the “active” list
                    if (_activeChallenges.All(c => c.Id != kvp.Key))
                    {
                        _activeChallenges.Add(challenge);
                        newlyActivated.Add(challenge);
                    }
                }
            }

            return newlyActivated;
        }
        private bool IsLogForChallenge(ParsedLogEntry log, DataStructures.Challenge challenge)
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
            _activeChallenges.Clear();
            _challenges.ForEach(c => c.Reset());
            Dispatcher.UIThread.Invoke(() =>
            {
                _challenges.Clear();
            });
        }

        public void UpdateCombats(Combat obj)
        {
            UpdateChallengesWithPhases();
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
