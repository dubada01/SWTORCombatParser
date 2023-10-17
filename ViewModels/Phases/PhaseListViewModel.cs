using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.Views.Challenges;
using SWTORCombatParser.Views.Phases;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Phases
{
    public class PhaseListViewModel:INotifyPropertyChanged
    {
        private EncounterSelectionViewModel _enounterSelectionViewModel;
        private string selectedTimerSource;
        private Phase _phaseEdited;
        private IEnumerable<Phase> _savedChallengeData;

        public event PropertyChangedEventHandler PropertyChanged;
        public static event Action PhasesUpdated = delegate { };
        public EncounterSelectionView EncounterSelectionView { get; set; }
        public PhaseListViewModel() {
            EncounterSelectionView = EncounterSelectionFactory.GetEncounterSelectionView(false);
            _enounterSelectionViewModel = EncounterSelectionView.DataContext as EncounterSelectionViewModel;
            _enounterSelectionViewModel.SelectionUpdated += UpdateSelectedEncounter;
            UpdateSelectedEncounter(_enounterSelectionViewModel.SelectedEncounter.Name, _enounterSelectionViewModel.SelectedBoss);
        }
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
                UpdatePhaseRows();
            }
        }

        public ObservableCollection<PhaseRowViewModel> PhaseRows { get; private set; }
        public ICommand AddPhaseCommand => new CommandHandler(CreateNewPhase);

        private void CreateNewPhase(object obj)
        {
            var vm = new PhaseModificationViewModel(SelectedSource);
            vm.OnNewPhase += NewPhase;
            var t = new PhaseModificationView(vm);
            t.ShowDialog();
        }

        private void CancelEdit(Phase editedChallenge)
        {
            var addedBack = new PhaseRowViewModel() { SourcePhase = editedChallenge };
            addedBack.EditRequested += Edit;
            addedBack.DeleteRequested += Delete;
            PhaseRows.Add(addedBack);
            UpdateRowColors();
        }
        private void NewPhase(Phase obj)
        {
            DefaultPhaseManager.AddOrUpdatePhase(obj);
            var newTimer = new PhaseRowViewModel() { SourcePhase = obj};
            newTimer.EditRequested += Edit;
            newTimer.DeleteRequested += Delete;
            PhaseRows.Add(newTimer);
            UpdateRowColors();
            PhasesUpdated();
        }

        private void UpdatePhaseRows()
        {
            _savedChallengeData = DefaultPhaseManager.GetExisitingPhases();
            var allValidPhases = _savedChallengeData.Where(t => SelectedSource.Contains('|') ? CompareEncounters(t.PhaseSource, SelectedSource) : t.PhaseSource == SelectedSource).ToList();
            List<PhaseRowViewModel> phaseObjects = new List<PhaseRowViewModel>();
            if (allValidPhases.Count() == 0)
                phaseObjects = new List<PhaseRowViewModel>();
            if (allValidPhases.Count() == 1)
                phaseObjects = new List<PhaseRowViewModel>() { new PhaseRowViewModel() { SourcePhase = allValidPhases[0] } };
            if (allValidPhases.Count() > 1)
                phaseObjects = allValidPhases.Select(s => new PhaseRowViewModel() { SourcePhase = s }).ToList();

            phaseObjects.ForEach(t => t.EditRequested += Edit);
            phaseObjects.ForEach(t => t.DeleteRequested += Delete);
            App.Current.Dispatcher.Invoke(() =>
            {
                PhaseRows = new ObservableCollection<PhaseRowViewModel>(phaseObjects);
                OnPropertyChanged("PhaseRows");
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
        private void Delete(PhaseRowViewModel obj)
        {
            DefaultPhaseManager.RemovePhase(obj.SourcePhase);
            PhaseRows.Remove(obj);
            UpdateRowColors();
            PhasesUpdated();
        }

        private void Edit(PhaseRowViewModel obj)
        {
            _phaseEdited = obj.SourcePhase;
            PhaseRows.Remove(obj);
            var vm = new PhaseModificationViewModel(SelectedSource);
            vm.OnNewPhase += NewPhase;
            vm.OnCancelEdit += CancelEdit;
            var t = new PhaseModificationView(vm);
            vm.Edit(_phaseEdited);
            t.ShowDialog();
        }
        private void UpdateRowColors()
        {
            for (var i = 0; i < PhaseRows.Count; i++)
            {
                if (i % 2 == 1)
                {
                    PhaseRows[i].RowBackground = Brushes.Gray;
                }
                else
                {
                    PhaseRows[i].RowBackground = Brushes.Transparent;
                }
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
