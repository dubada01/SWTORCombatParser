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
        // Fields
        public static ChallengeWindowViewModel _challengeWindowViewModel;
        private EncounterSelectionViewModel _encounterSelectionViewModel;
        private bool _isLocked;
        private Challenge _editedChallenge;
        private string _selectedTimerSource;
        private List<DefaultChallengeData> _savedChallengeData;
        private ObservableCollection<ChallengeRowViewModel> _challengeRows = new();
        private bool _areChallengesEnabled;

        // Properties
        public EncounterSelectionView EncounterSelectionView { get; set; }
        public string ImportId { get; set; }
        public ObservableCollection<ChallengeRowViewModel> ChallengeRows
        {
            get => _challengeRows;
            set => this.RaiseAndSetIfChanged(ref _challengeRows, value);
        }
        public string SelectedSource
        {
            get => _selectedTimerSource;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTimerSource, value);
                UpdateChallengeRows();
            }
        }
        public bool AreChallengesEnabled
        {
            get => _areChallengesEnabled;
            set
            {
                this.RaiseAndSetIfChanged(ref _areChallengesEnabled, value);
                DefaultBossFrameManager.SetRaidChallenges(_areChallengesEnabled);
            }
        }

        // Events
        public event Action ChallengesDisabled = delegate { };

        // Commands
        public ReactiveCommand<object, Unit> ImportCommand => ReactiveCommand.Create<object>(Import);
        public ReactiveCommand<Unit, Unit> AllChallengeCommand => ReactiveCommand.Create(CreateNewChallenge);

        // Constructor
        public ChallengeSetupViewModel()
        {
            AreChallengesEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
            DefaultBossFrameManager.DefaultsUpdated += UpdateChallengeEnabled;
            EncounterSelectionView = EncounterSelectionFactory.GetEncounterSelectionView(false);
            _encounterSelectionViewModel = EncounterSelectionView.DataContext as EncounterSelectionViewModel;
            _encounterSelectionViewModel.SelectionUpdated += UpdateSelectedEncounter;
            _challengeWindowViewModel = new ChallengeWindowViewModel("Challenges");
            _challengeWindowViewModel.CloseRequested += () =>
            {
                AreChallengesEnabled = false;
                ChallengesDisabled();
            };
            RefreshEncounterSelection();
        }

        // Methods
        public void CombatSelected(Combat selectedCombat) => _challengeWindowViewModel.CombatSelected(selectedCombat);

        public void CombatUpdated(Combat combat) => _challengeWindowViewModel.CombatUpdated(combat);

        public void UpdateLock(bool state)
        {
            _challengeWindowViewModel.UpdateLock(state);
            _isLocked = state;
        }

        public void SetScalar(double sizeScalar) => _challengeWindowViewModel.SetScale(sizeScalar);

        public void RefreshEncounterSelection()
        {
            if (CombatIdentifier.CurrentCombat != null && CombatIdentifier.CurrentCombat.IsCombatWithBoss)
            {
                _encounterSelectionViewModel.SelectedEncounter = _encounterSelectionViewModel.AvailableEncounters.FirstOrDefault(e => e.Name == CombatIdentifier.CurrentCombat.ParentEncounter.Name);
                _encounterSelectionViewModel.SelectedBoss = CombatIdentifier.CurrentCombat.EncounterBossDifficultyParts.Item1;
            }
            UpdateSelectedEncounter(_encounterSelectionViewModel.SelectedEncounter.Name, _encounterSelectionViewModel.SelectedBoss);
        }

        private void UpdateSelectedEncounter(string encounterName, string bossName)
        {
            SelectedSource = encounterName + "|" + bossName;
        }

        private void UpdateChallengeEnabled()
        {
            AreChallengesEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
        }

        private void UpdateChallengeRows()
        {
            _savedChallengeData = DefaultChallengeManager.GetAllDefaults();
            var allValidChallenges = _savedChallengeData.Where(t => SelectedSource.Contains('|') ? CompareEncounters(t.ChallengeSource, SelectedSource) : t.ChallengeSource == SelectedSource).ToList();
            var timerObjects = allValidChallenges.SelectMany(s => s.Challenges.Select(t => new ChallengeRowViewModel { SourceChallenge = t, IsEnabled = t.IsEnabled })).ToList();

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
            if (!encounter1.Contains("|")) return false;
            var parts = encounter1.Split('|');
            var encounterWithoutDiff = string.Join("|", parts[0], parts[1]);
            return encounter2 == encounterWithoutDiff;
        }

        private void Import(object obj)
        {
            ChallengeDatabaseAccess.GetChallengeFromId(ImportId).ContinueWith(task => NewChallenge(task.Result, false));
        }

        private void Share(ChallengeRowViewModel obj)
        {
            TimerDatabaseAccess.GetAllTimerIds().ContinueWith(async task =>
            {
                var currentIds = task.Result;
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
            });
        }

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
            var addedBack = new ChallengeRowViewModel { SourceChallenge = editedChallenge, IsEnabled = editedChallenge.IsEnabled };
            addedBack.EditRequested += Edit;
            addedBack.ShareRequested += Share;
            addedBack.DeleteRequested += Delete;
            ChallengeRows.Add(addedBack);
            UpdateRowColors();
        }

        private void NewChallenge(Challenge obj, bool wasEdit)
        {
            if (wasEdit)
            {
                DefaultChallengeManager.RemoveChallengeFromSource(_editedChallenge);
            }
            SaveNewTimer(obj);
            var newTimer = new ChallengeRowViewModel { SourceChallenge = obj, IsEnabled = obj.IsEnabled };
            newTimer.EditRequested += Edit;
            newTimer.ShareRequested += Share;
            newTimer.DeleteRequested += Delete;
            ChallengeRows.Add(newTimer);
            UpdateRowColors();
            _challengeWindowViewModel.RefreshChallenges();
        }

        private void SaveNewTimer(Challenge timer)
        {
            DefaultChallengeManager.AddChallengesToSource(new List<Challenge> { timer }, SelectedSource);
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
            _editedChallenge = obj.SourceChallenge;
            ChallengeRows.Remove(obj);
            var vm = new ChallengeModificationViewModel(SelectedSource);
            vm.OnNewChallenge += NewChallenge;
            vm.OnCancelEdit += CancelEdit;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var t = new ChallengeModificationView(vm);
                vm.Edit(_editedChallenge);
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
                ChallengeRows[i].RowBackground = new SolidColorBrush(i % 2 == 1 ? Brushes.DimGray.Color : Brushes.Transparent.Color);
            }
        }
    }
}
