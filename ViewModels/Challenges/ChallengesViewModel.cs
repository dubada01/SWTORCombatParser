using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.Views.Challenges;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Challenges
{
    public class ChallengeSetupViewModel : INotifyPropertyChanged
    {
        public static ChallengeWindowViewModel _challengeWindowViewModel;
        private EncounterSelectionViewModel _enounterSelectionViewModel;
        private bool _isLocked;
        private Challenge _challengeEdited;
        private string selectedTimerSource;
        private List<DefaultChallengeData> _savedChallengeData;
        private ObservableCollection<ChallengeRowViewModel> challengeRows = new ObservableCollection<ChallengeRowViewModel>();
        private bool challengesEnabled;

        public event PropertyChangedEventHandler PropertyChanged;

        public EncounterSelectionView EncounterSelectionView { get; set; }
        public void UpdateSelectedEncounter(string encounterName, string bossName)
        {
            SelectedSource = encounterName + "|" + bossName;
        }
        public string SelectedSource
        {
            get => selectedTimerSource; set
            {
                selectedTimerSource = value;

                OnPropertyChanged();
                UpdateChallengeRows();
                OnPropertyChanged("VisibleTimerSelected");
            }
        }
        public bool ChallengesEnabled
        {
            get => challengesEnabled; set
            {
                challengesEnabled = value;
                DefaultBossFrameManager.SetRaidChallenges(challengesEnabled);
                OnPropertyChanged();
            }
        }
        public ObservableCollection<ChallengeRowViewModel> ChallengeRows
        {
            get => challengeRows; set
            {
                challengeRows = value;
                OnPropertyChanged();
            }
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
            _challengeWindowViewModel = new ChallengeWindowViewModel();
            UpdateSelectedEncounter(_enounterSelectionViewModel.SelectedEncounter.Name, _enounterSelectionViewModel.SelectedBoss);
        }

        private void UpdateChallengeEnabled()
        {
            ChallengesEnabled = DefaultBossFrameManager.GetDefaults().RaidChallenges;
        }

        public ICommand AllChallengeCommand => new CommandHandler(CreateNewChallenge);

        private void CreateNewChallenge(object obj)
        {
            var vm = new ChallengeModificationViewModel(SelectedSource);
            vm.OnNewChallenge += NewChallenge;
            var t = new ChallengeModificationView(vm);
            t.ShowDialog();
        }

        private void CancelEdit(Challenge editedChallenge)
        {
            var addedBack = new ChallengeRowViewModel() { SourceChallenge = editedChallenge, IsEnabled = editedChallenge.IsEnabled };
            addedBack.EditRequested += Edit;
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
            timerObjects.ForEach(t => t.ActiveChanged += ActiveChanged);
            App.Current.Dispatcher.Invoke(() =>
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
            var t = new ChallengeModificationView(vm);
            vm.Edit(_challengeEdited);
            t.ShowDialog();
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
                    ChallengeRows[i].RowBackground = Brushes.Gray;
                }
                else
                {
                    ChallengeRows[i].RowBackground = Brushes.Transparent;
                }
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void SetScalar(double sizeScalar)
        {
            _challengeWindowViewModel.SetScale(sizeScalar);
        }
    }
}
