using System;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Timers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeSetupViewModel : ReactiveObject
    {
        public static ChallengeWindowViewModel _challengeWindowViewModel;
        private EncounterSelectionViewModel _enounterSelectionViewModel;
        private bool _isLocked;
        private Challenge _challengeEdited;
        private string selectedTimerSource;
        private List<DefaultChallengeData> _savedChallengeData;
        private ObservableCollection<ChallengeRowViewModel> challengeRows = new ObservableCollection<ChallengeRowViewModel>();
        private bool challengesEnabled;
        public event Action ChallengesDisabled = delegate { };
        public EncounterSelectionView EncounterSelectionView { get; set; }
        public void UpdateSelectedEncounter(string encounterName, string bossName)
        {
            SelectedSource = encounterName + "|" + bossName;
        }
        public string SelectedSource
        {
            get => selectedTimerSource; set
            {
                this.RaiseAndSetIfChanged(ref selectedTimerSource, value);
                UpdateChallengeRows();
            }
        }
        public bool ChallengesEnabled
        {
            get => challengesEnabled; set
            {
                this.RaiseAndSetIfChanged(ref challengesEnabled, value);
                DefaultBossFrameManager.SetRaidChallenges(challengesEnabled);
            }
        }
        public ObservableCollection<ChallengeRowViewModel> ChallengeRows
        {
            get => challengeRows; set => this.RaiseAndSetIfChanged(ref challengeRows, value);
        }
        public void CombatSelected(Combat selectedCombat)
        {
            _challengeWindowViewModel.CombatSelected(selectedCombat);
        }
        public void CombatUpdated(Combat combat)
        {
            _challengeWindowViewModel.CombatUpdated(combat);
        }
        public ChallengeSetupViewModel()
        {
            ChallengesEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            DefaultBossFrameManager.DefaultsUpdated += UpdateChallengeEnabled;
            EncounterSelectionView = EncounterSelectionFactory.GetEncounterSelectionView(false);
            _enounterSelectionViewModel = EncounterSelectionView.DataContext as EncounterSelectionViewModel;
            _enounterSelectionViewModel.SelectionUpdated += UpdateSelectedEncounter;
            _challengeWindowViewModel = new ChallengeWindowViewModel("Challenges");
            _challengeWindowViewModel.CloseRequested += () =>
            {
                ChallengesEnabled = false;
                ChallengesDisabled();
            };
            RefreshEncounterSelection();
        }

        public void RefreshEncounterSelection()
        {
            if (CombatIdentifier.CurrentCombat != null && CombatIdentifier.CurrentCombat.IsCombatWithBoss)
            {
                _enounterSelectionViewModel.SelectedEncounter = _enounterSelectionViewModel.AvailableEncounters.FirstOrDefault(e => e.Name == CombatIdentifier.CurrentCombat.ParentEncounter.Name);
                _enounterSelectionViewModel.SelectedBoss = CombatIdentifier.CurrentCombat.EncounterBossDifficultyParts.Item1;
            }
            UpdateSelectedEncounter(_enounterSelectionViewModel.SelectedEncounter.Name, _enounterSelectionViewModel.SelectedBoss);
        }

        private void UpdateChallengeEnabled()
        {
            ChallengesEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
        }
        public string ImportId { get; set; }
        public ReactiveCommand<object,Unit> ImportCommand => ReactiveCommand.Create<object>(Import);

        private async void Import(object obj)
        {
            var challenge = await ChallengeDatabaseAccess.GetChallengeFromId(ImportId);
            NewChallenge(challenge, false);
        }
        private async void Share(ChallengeRowViewModel obj)
        {
            var currentIds = await TimerDatabaseAccess.GetAllTimerIds();
            string id;
            do
            {
                id = AlphanumericsGenerator.RandomString(3);
            } while (currentIds.Contains(id));
            obj.SourceChallenge.ShareId = id;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var shareWindow = new TimerSharePopup(id);
                shareWindow.ShowDialog(desktop.MainWindow);
                DefaultChallengeManager.SetIdForChallenge(obj.SourceChallenge, SelectedSource, id);
                await ChallengeDatabaseAccess.AddChallenge(obj.SourceChallenge);
            }
        }
        public ReactiveCommand<Unit,Unit> AllChallengeCommand => ReactiveCommand.Create(CreateNewChallenge);

        private void CreateNewChallenge()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var vm = new ChallengeModificationViewModel(SelectedSource);
                vm.OnNewChallenge += NewChallenge;
                var t = new ChallengeModificationView(vm);
                t.ShowDialog(desktop.MainWindow);
            }
        }

        private void CancelEdit(Challenge editedChallenge)
        {
            var addedBack = new ChallengeRowViewModel() { SourceChallenge = editedChallenge, IsEnabled = editedChallenge.IsEnabled };
            addedBack.EditRequested += Edit;
            addedBack.ShareRequested += Share;
            addedBack.DeleteRequested += Delete;
            ChallengeRows.Add(addedBack);
            UpdateRowColors();
        }

        private void NewChallenge(Challenge obj, bool wasEdit)
        {
            if (wasEdit)
                DefaultChallengeManager.RemoveChallengeFromSource(_challengeEdited);
            SaveNewTimer(obj);
            var newTimer = new ChallengeRowViewModel() { SourceChallenge = obj, IsEnabled = obj.IsEnabled };
            newTimer.EditRequested += Edit;
            newTimer.ShareRequested += Share;
            newTimer.DeleteRequested += Delete;
            ChallengeRows.Add(newTimer);
            UpdateRowColors();
            _challengeWindowViewModel.RefreshChallenges();
        }
        private void SaveNewTimer(Challenge timer)
        {
            DefaultChallengeManager.AddChallengesToSource(new List<Challenge>() { timer }, SelectedSource);
        }
        public void UpdateLock(bool state)
        {
            _challengeWindowViewModel.UpdateLock(state);
            _isLocked = state;
        }
        private void UpdateChallengeRows()
        {
            _savedChallengeData = DefaultChallengeManager.GetAllDefaults();
            var allValidChallenges = _savedChallengeData.Where(t => SelectedSource.Contains('|') ? CompareEncounters(t.ChallengeSource, SelectedSource) : t.ChallengeSource == SelectedSource).ToList();
            List<ChallengeRowViewModel> timerObjects = new List<ChallengeRowViewModel>();
            if (allValidChallenges.Count() == 0)
                timerObjects = new List<ChallengeRowViewModel>();
            if (allValidChallenges.Count() == 1)
                timerObjects = allValidChallenges[0].Challenges.Select(t => new ChallengeRowViewModel() { SourceChallenge = t, IsEnabled = t.IsEnabled }).ToList();
            if (allValidChallenges.Count() > 1)
                timerObjects = allValidChallenges.SelectMany(s => s.Challenges.Select(t => new ChallengeRowViewModel() { SourceChallenge = t, IsEnabled = t.IsEnabled }).ToList()).ToList();

            timerObjects.ForEach(t => t.EditRequested += Edit);
            timerObjects.ForEach(t => t.DeleteRequested += Delete);
            timerObjects.ForEach(t => t.ShareRequested += Share);
            timerObjects.ForEach(t => t.ActiveChanged += ActiveChanged);
            Dispatcher.UIThread.Invoke(() =>
            {
                ChallengeRows = new ObservableCollection<ChallengeRowViewModel>(timerObjects);
                UpdateRowColors();
            });


        }
        private bool CompareEncounters(string encounter1, string encounter2)
        {
            if (!encounter1.Contains("|"))
                return false;
            var parts = encounter1.Split('|');
            var encounterWithoutDiff = string.Join("|", parts[0], parts[1]);
            return encounter2 == encounterWithoutDiff;
        }
        private void Delete(ChallengeRowViewModel obj)
        {
            DefaultChallengeManager.RemoveChallengeFromSource(obj.SourceChallenge);
            ChallengeRows.Remove(obj);
            _challengeWindowViewModel.RefreshChallenges();
            UpdateRowColors();
        }

        private void Edit(ChallengeRowViewModel obj)
        {
            _challengeEdited = obj.SourceChallenge;
            ChallengeRows.Remove(obj);
            var vm = new ChallengeModificationViewModel(SelectedSource);
            vm.OnNewChallenge += NewChallenge;
            vm.OnCancelEdit += CancelEdit;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var t = new ChallengeModificationView(vm);
                vm.Edit(_challengeEdited);
                t.ShowDialog(desktop.MainWindow);
            }
        }
        private void ActiveChanged(ChallengeRowViewModel timerRow)
        {
            DefaultChallengeManager.SetChallengeEnabled(timerRow.IsEnabled, timerRow.SourceChallenge);
            _challengeWindowViewModel.RefreshChallenges();
        }
        private void UpdateRowColors()
        {
            for (var i = 0; i < ChallengeRows.Count; i++)
            {
                if (i % 2 == 1)
                {
                    ChallengeRows[i].RowBackground = new SolidColorBrush(Brushes.DimGray.Color);
                }
                else
                {
                    ChallengeRows[i].RowBackground = new SolidColorBrush(Brushes.Transparent.Color);
                }
            }
        }
        internal void SetScalar(double sizeScalar)
        {
            _challengeWindowViewModel.SetScale(sizeScalar);
        }
    }
}
