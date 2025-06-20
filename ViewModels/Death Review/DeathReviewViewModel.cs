using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.BattleReview;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.Views.Battle_Review;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Death_Review;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Death_Review
{
    public class DeathReviewViewModel : INotifyPropertyChanged
    {
        public ChallengeControl DeathChallengeView { get; set; }
        private DeathChallengeViewModel _challengeViewModel;
        public EventHistoryView DeathLogsView { get; set; }
        private EventHistoryViewModel _deathLogsViewModel;
        public LegacyDeathPlot DeathPlotView { get; set; }
        private LegacyDeathReviewPlotVM _plotViewModel;
        public DeathPlayerList DeathPlayerListView { get; set; }
        private DeathPlayerListViewModel _playerListViewModel;

        public event PropertyChangedEventHandler PropertyChanged;
        private Combat _currentCombat;

        public DeathReviewViewModel()
        {
            DeathChallengeView = new ChallengeControl();
            DeathChallengeView.DataContext = ChallengeSetupViewModel._challengeWindowViewModel;

            _deathLogsViewModel = new EventHistoryViewModel();
            _deathLogsViewModel.SetDisplayType(DisplayType.DeathRecap);
            _deathLogsViewModel.LogPositionChanged += TryUpdateGraph;
            DeathLogsView = new EventHistoryView(_deathLogsViewModel);

            _plotViewModel = new LegacyDeathReviewPlotVM();
            DeathPlotView = new LegacyDeathPlot(_plotViewModel);
            Observable.FromEvent<double>(
                handler => _plotViewModel.XValueSelected += handler,
                handler => _plotViewModel.XValueSelected -= handler).Sample(TimeSpan.FromSeconds(0.1)).Subscribe(SeekToPosition);

            _playerListViewModel = new DeathPlayerListViewModel();
            _playerListViewModel.ParticipantSelected += list => _ = UpdateSelectedPlayers(list);
            DeathPlayerListView = new DeathPlayerList(_playerListViewModel);
        }

        private void TryUpdateGraph(double obj, List<EntityInfo> currentEntityInfo)
        {
            _playerListViewModel.SetEntityHPS(currentEntityInfo);
        }

        private void SeekToPosition(double obj)
        {
            List<EntityInfo> mostRecentInfos = _deathLogsViewModel.Seek(obj);
            _playerListViewModel.SetEntityHPS(mostRecentInfos);
        }

        private async Task UpdateSelectedPlayers(List<Entity> obj)
        {
            _deathLogsViewModel.SetViewableEntities(obj);
            var startTime = await _deathLogsViewModel.UpdateLogs(true);

            _plotViewModel.Reset();
            _plotViewModel.PlotCombat(_currentCombat, obj, startTime);
        }

        internal void Reset()
        {
            _playerListViewModel.Reset();
            _plotViewModel.Reset();
        }

        internal void AddCombat(Combat selectedCombat)
        {
            _currentCombat = selectedCombat;
            _deathLogsViewModel.SelectCombat(selectedCombat, true);
            Reset();
            var playersThatDidNotStuck = selectedCombat.AllLogs.Values.Where(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && !string.IsNullOrEmpty(l.Source.Name) && l.Target.IsCharacter).Select(l => l.Target).DistinctBy(p => p.LogId).ToList();
            var selectedParticipants = _playerListViewModel.UpdateParticipantsData(selectedCombat, playersThatDidNotStuck);
            if (playersThatDidNotStuck.Any())
            {
                _deathLogsViewModel.SetViewableEntities(selectedParticipants.ToList());
            }
            
            //_plotViewModel.Reset();
            //_plotViewModel.PlotCombat(_currentCombat, selectedParticipants, startTime);
        }

        internal void RemoveCombat(Combat obj)
        {
            //throw new NotImplementedException();
        }
    }
}
