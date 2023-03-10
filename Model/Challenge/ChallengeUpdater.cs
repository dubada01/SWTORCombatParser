using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
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
        private (string, string, string) _currentBossInfo;
        private EncounterInfo _currentEncounter;

        public ChallengeUpdater()
        {
            CombatIdentifier.NewCombatStarted += ResetChallenges;
            CombatIdentifier.NewCombatAvailable += UpdateCombats;
            CombatSelectionMonitor.NewCombatSelected += CombatSelected;
            CombatLogStreamer.NewLineStreamed += CheckForActiveChallenge;

            CombatIdentifier.NewBossCombatDetected += SetBossInfo;
            CombatLogStateBuilder.AreaEntered += EncounterChanged;

            RefreshChallenges();
        }

        private void EncounterChanged(EncounterInfo obj)
        {
            _currentEncounter = obj;
        }

        private void SetBossInfo((string, string, string) arg1, DateTime arg2)
        {
            _currentBossInfo = arg1;
        }

        public void RefreshChallenges()
        {
            _allChallenges = DefaultChallengeManager.GetAllDefaults().SelectMany(c => c.Challenges).Where(c=>c.IsEnabled).ToList();
        }
        private void CheckForActiveChallenge(ParsedLogEntry obj)
        {
            foreach(var challenge in _allChallenges)
            {
                if(IsLogForChallenge(obj,challenge) && (_currentBossInfo.Item1 == challenge.Source.Split('|')[1]))
                {
                    if (!_activeChallenges.Any(c => c.Id == challenge.Id))
                    { 
                        _activeChallenges.Add(challenge);
                        App.Current.Dispatcher.Invoke(() => {
                            _challenges.Add(new ChallengeInstanceViewModel(challenge));
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
                        return (log.Ability == challenge.Value || log.AbilityId == challenge.Value || string.IsNullOrEmpty(challenge.Value)) && (log.Target.Name == challenge.ChallengeTarget || log.Target.LogId.ToString() == challenge.ChallengeTarget);
                    }
                case ChallengeType.DamageIn:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId == challenge.Value || string.IsNullOrEmpty(challenge.Value)) && (log.Source.Name == challenge.ChallengeSource || log.Source.LogId.ToString() == challenge.ChallengeSource);
                    }
                case ChallengeType.AbilityCount:
                    {
                        return (log.Ability == challenge.Value || log.AbilityId == challenge.Value);
                    }
                case ChallengeType.InterruptCount:
                    {
                        return log.AbilityId == _7_0LogParsing.InterruptCombatId;
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
            _currentBossInfo = obj.EncounterBossDifficultyParts;
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
