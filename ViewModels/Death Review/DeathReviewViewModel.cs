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
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Death_Review
{
    public class DeathReviewViewModel : INotifyPropertyChanged, IDisposable
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
        private bool _initializingCombat;
        private int _selectionVersion;
        private readonly IDisposable _positionSubscription;

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
            _positionSubscription = Observable.FromEvent<double>(
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
            if (_currentCombat == null) return;
            List<EntityInfo> mostRecentInfos = _deathLogsViewModel.Seek(obj);
            _playerListViewModel.SetEntityHPS(mostRecentInfos);
        }

        private async Task UpdateSelectedPlayers(List<Entity> obj)
        {
            if (_initializingCombat || _currentCombat == null)
                return;
            var version = Interlocked.Increment(ref _selectionVersion);
            var combat = _currentCombat;
            _deathLogsViewModel.SetViewableEntities(obj);
            var startTime = await _deathLogsViewModel.SelectCombat(combat, true);

            if (version != Volatile.Read(ref _selectionVersion))
                return;

            _plotViewModel.Reset();
            _plotViewModel.PlotCombat(combat, obj, startTime);
        }

        internal void Reset()
        {
            Interlocked.Increment(ref _selectionVersion);
            _playerListViewModel.Reset();
            _plotViewModel.Reset();
        }

        internal void AddCombat(Combat selectedCombat)
        {
            _currentCombat = selectedCombat;
            Reset();
            var playersThatDidNotStuck = selectedCombat.AllLogs.Values.Where(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && !string.IsNullOrEmpty(l.Source.Name) && l.Target.IsCharacter).Select(l => l.Target).DistinctBy(p => p.LogId).ToList();
            List<Entity> selectedParticipants;
            _initializingCombat = true;
            try
            {
                // Selecting the initial player raises ParticipantSelected. Wait until
                // the new combat and its player list are both ready before updating.
                selectedParticipants = _playerListViewModel.UpdateParticipantsData(selectedCombat, playersThatDidNotStuck).ToList();
            }
            finally
            {
                _initializingCombat = false;
            }
            _ = UpdateSelectedPlayers(selectedParticipants);
        }

        public void Dispose()
        {
            _positionSubscription.Dispose();
            _currentCombat = null;
            _deathLogsViewModel.ClearCombat();
            Reset();
        }
    }
}
