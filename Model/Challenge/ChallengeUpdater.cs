using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SWTORCombatParser.Model.Challenge
{
    public class ChallengeUpdater
    {
        private ObservableCollection<ChallengeInstanceViewModel> _challenges;
        private List<DataStructures.Challenge> _activeChallenges = new List<DataStructures.Challenge>();
        private List<DataStructures.Challenge> _allChallenges = new List<DataStructures.Challenge>();
        private string _currentBossName;
        private Combat _currentSelectedCombat;
        private EncounterInfo _currentEncounter;
        private double _currentScale = 1;
        public ChallengeUpdater()
        {
            CombatLogStreamer.CombatStarted += ResetChallenges;
            CombatLogStreamer.NewLineStreamed += CheckForActiveChallenge;

            EncounterTimerTrigger.EncounterDetected += SetBossInfo;
            CombatLogStateBuilder.AreaEntered += EncounterChanged;
            RefreshChallenges();
        }

        private void UpdateChallengesWithPhases()
        {
            if(!PhaseManager.ActivePhases.Any())
                _challenges.ForEach(c=>c.UpdatePhase(null));
            foreach (var phaseChallenge in _challenges.Where(c => c.Type == ChallengeType.MetricDuringPhase))
            {
                phaseChallenge.UpdatePhase(phaseChallenge.SourceChallenge.PhaseId == Guid.Empty ? 
                    new List<PhaseInstance>() : 
                    PhaseManager.ActivePhases.Where(p => p.SourcePhase.Id == phaseChallenge.SourceChallenge.PhaseId).ToList());
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
            _allChallenges = DefaultChallengeManager.GetAllDefaults().SelectMany(c => c.Challenges).Where(c => c.IsEnabled).ToList();
        }
        private void CheckForActiveChallenge(ParsedLogEntry obj)
        {
            foreach (var challenge in _allChallenges)
            {
                if (IsLogForChallenge(obj, challenge) && (_currentBossName == challenge.Source.Split('|')[1]) || 
                    (challenge.ChallengeType == ChallengeType.MetricDuringPhase && PhaseManager.ActivePhases.Any(p => challenge.PhaseId == p.SourcePhase.Id)))
                {
                    if (!_activeChallenges.Any(c => c.Id == challenge.Id))
                    {
                        _activeChallenges.Add(challenge);
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            _challenges.Add(new ChallengeInstanceViewModel(challenge) { Scale = _currentScale });
                        });
                    }
                }
            }
        }
        private bool IsLogForChallenge(ParsedLogEntry log, DataStructures.Challenge challenge)
        {
            switch (challenge.ChallengeType)
            {
                case ChallengeType.DamageOut:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId == challenge.Value || string.IsNullOrEmpty(challenge.Value)) && (log.Target.Name == challenge.ChallengeTarget || log.Target.LogId.ToString() == challenge.ChallengeTarget || string.IsNullOrEmpty(challenge.ChallengeTarget));
                    }
                case ChallengeType.DamageIn:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId == challenge.Value || string.IsNullOrEmpty(challenge.Value)) && (log.Source.Name == challenge.ChallengeSource || log.Source.LogId.ToString() == challenge.ChallengeSource || string.IsNullOrEmpty(challenge.ChallengeSource));
                    }
                case ChallengeType.AbilityCount:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId == challenge.Value);
                    }
                case ChallengeType.InterruptCount:
                    {
                        return log.AbilityId == _7_0LogParsing.InterruptCombatId;
                    }
                case ChallengeType.EffectStacks:
                    {
                        return log.Effect.EffectName == challenge.Value || log.Effect.EffectId == challenge.Value;
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
            _currentBossName = obj.EncounterBossDifficultyParts.Item1;
            _currentSelectedCombat = obj;
            ResetChallenges();
            foreach (var log in obj.AllLogs)
            {
                CheckForActiveChallenge(log);
            }
            UpdateCombats(obj);
        }
        private void ResetChallenges()
        {
            _activeChallenges.Clear();
            _challenges.ForEach(c => c.Reset());
            App.Current.Dispatcher.Invoke(() =>
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
                if(activeChallenge != null)
                    activeChallenge.UpdateMetrics(obj, challenge);
            }
        }
    }
}
