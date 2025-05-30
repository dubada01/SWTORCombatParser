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
        private object _selectionLock = new object();
        private object _updateLock = new object();
        public ChallengeUpdater()
        {
            CombatLogStreamer.CombatStarted += ResetChallenges;
            CombatLogStreamer.NewLineStreamed += CheckForActiveChallenge;

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

            lock (_updateLock)
            {
                _allChallenges = newDict;
            }
        }
        private void CheckForActiveChallenge(ParsedLogEntry obj)
        {
            var toAddViewModels = new List<DataStructures.Challenge>();

            lock (_updateLock)
            {
                foreach (var kvp in _allChallenges)
                {
                    var challenge = kvp.Value;

                    bool logMatch      = IsLogForChallenge(obj, challenge);
                    bool bossMatch     = _currentBossName == challenge.Source.Split('|')[1];
                    bool phaseMatch    = challenge.ChallengeType == ChallengeType.MetricDuringPhase
                                         && PhaseManager.ActivePhases.Any(p => p.SourcePhase.Id == challenge.PhaseId);

                    // require the log to match, plus either boss or phase criteria
                    if ((logMatch && bossMatch) || phaseMatch)
                    {
                        if (_activeChallenges.All(c => c.Id != kvp.Key))
                        {
                            _activeChallenges.Add(challenge);
                            toAddViewModels.Add(challenge);
                        }
                    }
                }
            }

            // now update the UI *after* releasing _updateLock
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var c in toAddViewModels)
                {
                    _challenges.Add(new ChallengeInstanceViewModel(c)
                    {
                        Scale = _currentScale
                    });
                }
            });
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
        public void CombatSelected(Combat obj)
        {
            lock (_selectionLock)
            {
                _currentBossName = obj.EncounterBossDifficultyParts.Item1;
                _currentSelectedCombat = obj;
                ResetChallenges();
                var snapshot = obj.AllLogs.ToList(); // Preserves insertion/enumeration order
                foreach (var log in snapshot)
                {
                    CheckForActiveChallenge(log);
                }
                UpdateCombats(obj);
            }
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
