using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Challenge
{
    public class ChallengeUpdater
    {
        private ObservableCollection<ChallengeInstanceViewModel> _challenges;
        private List<DataStructures.Challenge> _activeChallenges = new List<DataStructures.Challenge>();
        private List<DataStructures.Challenge> _allChallenges = new List<DataStructures.Challenge>();
        private Combat _currentCombat;
        private string _currentBossName;
        private EncounterInfo _currentEncounter;
        private double _currentScale = 1;

        public ChallengeUpdater()
        {
            CombatIdentifier.NewCombatStarted += ResetChallenges;
            CombatIdentifier.NewCombatAvailable += UpdateCombats;
            CombatSelectionMonitor.NewCombatSelected += CombatSelected;
            CombatLogStreamer.NewLineStreamed += CheckForActiveChallenge;

            EncounterTimerTrigger.EncounterDetected += SetBossInfo;
            CombatLogStateBuilder.AreaEntered += EncounterChanged;
            RefreshChallenges();
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
            _allChallenges = DefaultChallengeManager.GetAllDefaults().SelectMany(c => c.Challenges).Where(c=>c.IsEnabled).ToList();
        }
        private void CheckForActiveChallenge(ParsedLogEntry obj)
        {
            foreach(var challenge in _allChallenges)
            {
                if(IsLogForChallenge(obj,challenge) && (_currentBossName == challenge.Source.Split('|')[1]))
                {
                    if (!_activeChallenges.Any(c => c.Id == challenge.Id))
                    { 
                        _activeChallenges.Add(challenge);
                        App.Current.Dispatcher.Invoke(() => {
                            _challenges.Add(new ChallengeInstanceViewModel(challenge) { Scale = _currentScale});
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
        private void CombatSelected(Combat obj)
        {
            _currentBossName = obj.EncounterBossDifficultyParts.Item1;
            ResetChallenges();
            foreach(var log in obj.AllLogs)
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

        private void UpdateCombats(Combat obj)
        {
            _currentCombat= obj;
            var activeChallenges = _activeChallenges.ToList();
            foreach (var challenge in activeChallenges)
            {
                var activeChallenge = _challenges.First(c => c.SourceChallengeId == challenge.Id);
                activeChallenge.UpdateMetrics(obj, challenge);
            }
        }
    }
}
