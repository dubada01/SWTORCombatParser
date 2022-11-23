using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.Boss_Timers;
using SWTORCombatParser.DataStructures.HOT_Timers;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Encounter_Selection;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Timers
{
    public enum TimerType
    {
        Encounter,
        Discipline
    }
    public class TimersCreationViewModel : INotifyPropertyChanged
    {
        private TimersWindowViewModel _disciplineTimersWindow;
        private TimersWindowViewModel _alertTimersWindow;
        private EncounterSelectionViewModel _enounterSelectionViewModel;
        private TimerType selectedTimerSourceType = TimerType.Discipline;
        private bool _isLocked;
        private Timer _timerEdited;
        private bool disciplineTimersActive;

        private List<DefaultTimersData> _savedTimersData = new List<DefaultTimersData>();

        public event PropertyChangedEventHandler PropertyChanged;

        public EncounterSelectionView EncounterSelectionView { get; set; }
        public void UpdateSelectedEncounter(string encounterName, string bossName, string difficulty)
        {
            SelectedTimerSource = encounterName + "|" + bossName + "|" + difficulty;
        }
        public List<TimerType> TimerSourcesTypes { get; set; } = new List<TimerType> { TimerType.Discipline, TimerType.Encounter};

        private string selectedTimerSource;

        public TimerType SelectedTimerSourceType
        {
            get => selectedTimerSourceType; set
            {
                selectedTimerSourceType = value;
                if (selectedTimerSourceType == TimerType.Encounter)
                {
                    AvailableTimerSources = EncounterTimersList;
                    var currentSelection = _enounterSelectionViewModel.GetCurrentSelection();
                    UpdateSelectedEncounter(currentSelection.Item1, currentSelection.Item2, currentSelection.Item3);
                }
                if (selectedTimerSourceType == TimerType.Discipline)
                {
                    AvailableTimerSources = DisciplineTimersList;
                    var mostRecentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(DateTime.Now);
                    if(mostRecentDiscipline != null)
                    {
                        var source = DisciplineTimersList.FirstOrDefault(v => v.Contains(mostRecentDiscipline.Discipline));
                        if (!string.IsNullOrEmpty(source))
                        {
                            SelectedTimerSource = source;
                        }

                    }
                    else
                    {
                        SelectedTimerSource = "Shared";
                    }
                }
                OnPropertyChanged("AvailableTimerSources");
                OnPropertyChanged("DisciplineTimerSelected");
                OnPropertyChanged("TimerActiveCheck");
            }
        }
        public bool DisciplineTimerSelected => SelectedTimerSourceType == TimerType.Discipline;
        public List<string> AvailableTimerSources { get; set; }
        public string SelectedTimerSource
        {
            get => selectedTimerSource; set
            {
                selectedTimerSource = value;

                OnPropertyChanged();
                UpdateTimerRows();
            }
        }
        public List<string> EncounterTimersList { get; set; } = new List<string>();
        public List<string> DisciplineTimersList { get; set; } = new List<string>();

        public bool DisciplineTimersActive
        {
            get => disciplineTimersActive; set
            {
                if (value == disciplineTimersActive)
                    return;
                disciplineTimersActive = value;
                if (disciplineTimersActive)
                {
                    _disciplineTimersWindow.Active = true;
                    if (CombatMonitorViewModel.IsLiveParseActive())
                    {
                        _disciplineTimersWindow.SetSource(SelectedTimerSource);
                        _disciplineTimersWindow.ShowTimers(_isLocked);
                    }
                }
                else
                {
                    _disciplineTimersWindow.Active = false;
                }
                DefaultTimersManager.UpdateTimersActive(DisciplineTimersActive,SelectedTimerSource);
                OnPropertyChanged();
            }
        }
       
        public void TryShow()
        {
            if (DisciplineTimersActive)
                _disciplineTimersWindow.ShowTimers(_isLocked);
        }
        public void HideTimers()
        {
            _disciplineTimersWindow.HideTimers();
            //_alertTimersWindow.HideTimers();
        }
        public ObservableCollection<TimerRowInstanceViewModel> TimerRows { get; set; } = new ObservableCollection<TimerRowInstanceViewModel>();

        public TimersCreationViewModel()
        {
            BossTimerLoader.TryLoadBossTimers();
            HotTimerLoader.TryLoadHots();
            EncounterSelectionView = EncounterSelectionFactory.GetEncounterSelectionView(false);
            _enounterSelectionViewModel = EncounterSelectionView.DataContext as EncounterSelectionViewModel;
            _enounterSelectionViewModel.SelectionUpdated += UpdateSelectedEncounter;
            _disciplineTimersWindow = new TimersWindowViewModel();
            _alertTimersWindow = new TimersWindowViewModel(true);
            CombatLogStateBuilder.PlayerDiciplineChanged += SetClass;
            CombatLogStreamer.HistoricalLogsFinished += SetDiscipline;
            RefreshAvaialbleTriggerOwners();
            if (DisciplineTimersList.Count > 0)
            {
                SelectedTimerSource = DisciplineTimersList[0];
                OnPropertyChanged("SelectedTimerSource");
            }
        }
        private void SetDiscipline(DateTime combatEndTime)
        {
            if (!CombatMonitorViewModel.IsLiveParseActive())
                return;
            var mostRecentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(combatEndTime);
            if (mostRecentDiscipline == null)
                return;
            var source = DisciplineTimersList.FirstOrDefault(v => v.Contains(mostRecentDiscipline.Discipline));
            if (source == null)
            {
                source = mostRecentDiscipline.Discipline;
                DisciplineTimersList.Add(source);
                DefaultTimersManager.UpdateTimersActive(false, source);
            }
            DisciplineTimersActive = DefaultTimersManager.GetTimersActive(source);
            OnPropertyChanged("DisciplineTimersActive");
            if (!string.IsNullOrEmpty(source))
            {
                SelectedTimerSource = source;
                _disciplineTimersWindow.SetSource(SelectedTimerSource);
            }
        }
        private void RefreshAvaialbleTriggerOwners()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            if (!_savedTimersData.Any(t => t.TimerSource == "Shared"))
            {
                _savedTimersData.Add(new DefaultTimersData() { TimerSource = "Shared" });
                DefaultTimersManager.SetSavedTimers(new List<Timer>(), "Shared");
            }
            var savedTimerSources = new List<string>(_savedTimersData.Where(t => t.TimerSource != null &&  !t.TimerSource.Contains('|')).Select(t => t.TimerSource));
            savedTimerSources = savedTimerSources.OrderBy(s => s).ToList();
            savedTimerSources.SwapItems(0, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "Shared")));
            savedTimerSources.SwapItems(1, savedTimerSources.IndexOf(savedTimerSources.First(t => t == "HOTS")));

            DisciplineTimersList = savedTimerSources;
            OnPropertyChanged("DisciplineTimersList");
        }

        public ICommand CreateNewTimerCommand => new CommandHandler(CreateNewTimer);

        private void CreateNewTimer(object obj)
        {
            var vm = new ModifyTimerViewModel(SelectedTimerSource);
            vm.OnNewTimer += NewTimer;
            ObscureWindowFactory.ShowObscureWindow();
            var t = new TimerModificationWindow(vm);
            t.Show();
        }
        public string ImportId { get; set; }
        public ICommand ImportCommand => new CommandHandler(Import);

        private void Import(object obj)
        {
            if (TimerRows.Any(t => t.SourceTimer.ShareId == ImportId))
                return;
            var timer = TimerDatabaseAccess.GetTimerFromId(ImportId);
            if (timer == null)
                return;
            timer.IsEnabled = true;
            NewTimer(timer, false);
        }

        private void CancelEdit()
        {
            var addedBack = new TimerRowInstanceViewModel() { SourceTimer = _timerEdited, IsEnabled = _timerEdited.IsEnabled };
            addedBack.EditRequested += Edit;
            addedBack.ShareRequested += Share;
            addedBack.DeleteRequested += Delete;
            TimerRows.Add(addedBack);
            UpdateRowColors();
        }

        private void NewTimer(Timer obj, bool wasEdit)
        {
            if (wasEdit)
                DefaultTimersManager.RemoveTimerForCharacter(_timerEdited, SelectedTimerSource);
            SaveNewTimer(obj);
            var newTimer = new TimerRowInstanceViewModel() { SourceTimer = obj, IsEnabled = obj.IsEnabled };
            newTimer.EditRequested += Edit;
            newTimer.ShareRequested += Share;
            newTimer.DeleteRequested += Delete;
            TimerRows.Add(newTimer);
            _disciplineTimersWindow.RefreshTimers();
            _alertTimersWindow.RefreshTimers();
            UpdateRowColors();
        }
        private void SaveNewTimer(Timer timer)
        {
            DefaultTimersManager.AddTimerForSource(timer, SelectedTimerSource);

        }
        public void UpdateLock(bool state)
        {
            _disciplineTimersWindow.UpdateLock(state);
            _alertTimersWindow.UpdateLock(state);
            _isLocked = state;
        }
        public void SetClass(Entity player, SWTORClass swtorclass)
        {
            if (!player.IsLocalPlayer || !CombatMonitorViewModel.IsLiveParseActive() || SelectedTimerSource == swtorclass.Discipline)
                return;
            _disciplineTimersWindow.SetPlayer(swtorclass);
            if (!DisciplineTimersList.Contains(swtorclass.Discipline))
            {
                DisciplineTimersList.Add(swtorclass.Discipline);
            }
            SelectedTimerSource = swtorclass.Discipline;

            _disciplineTimersWindow.SetSource(SelectedTimerSource);
            DisciplineTimersActive = DefaultTimersManager.GetTimersActive(SelectedTimerSource);

            RefreshAvaialbleTriggerOwners();
        }
        private void UpdateTimerRows()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            var timers = _savedTimersData.FirstOrDefault(t => t.TimerSource == SelectedTimerSource);
            if (timers == null)
                timers = new DefaultTimersData();
            var timerObjects = timers.Timers.Select(t => new TimerRowInstanceViewModel() { SourceTimer = t, IsEnabled = t.IsEnabled }).ToList();
            timerObjects.ForEach(t => t.EditRequested += Edit);
            timerObjects.ForEach(t => t.ShareRequested += Share);
            timerObjects.ForEach(t => t.DeleteRequested += Delete);
            timerObjects.ForEach(t => t.ActiveChanged += ActiveChanged);
            TimerRows = new ObservableCollection<TimerRowInstanceViewModel>(timerObjects);
            UpdateRowColors();
            OnPropertyChanged("TimerRows");
        }

        private void Delete(TimerRowInstanceViewModel obj)
        {
            DefaultTimersManager.RemoveTimerForCharacter(obj.SourceTimer, SelectedTimerSource);
            TimerRows.Remove(obj);
            _disciplineTimersWindow.RefreshTimers();
            _alertTimersWindow.RefreshTimers();
            UpdateRowColors();
        }

        private void Share(TimerRowInstanceViewModel obj)
        {
            var currentIds = TimerDatabaseAccess.GetAllTimerIds();
            string id;
            do
            {
                id = AlphanumericsGenerator.RandomString(3);
            } while (currentIds.Contains(id));
            obj.SourceTimer.ShareId = id;
            var shareWindow = new TimerSharePopup(id);
            shareWindow.Show();
            DefaultTimersManager.SetIdForTimer(obj.SourceTimer, SelectedTimerSource, id);
            TimerDatabaseAccess.AddTimer(obj.SourceTimer);
        }
        private void Edit(TimerRowInstanceViewModel obj)
        {
            _timerEdited = obj.SourceTimer;
            TimerRows.Remove(obj);
            var vm = new ModifyTimerViewModel(SelectedTimerSource);
            ObscureWindowFactory.ShowObscureWindow();
            vm.OnNewTimer += NewTimer;
            vm.OnCancelEdit += CancelEdit;
            var t = new TimerModificationWindow(vm);
            t.Show();
            vm.Edit(_timerEdited.Copy());
        }
        private void ActiveChanged(TimerRowInstanceViewModel timerRow)
        {
            DefaultTimersManager.SetTimerEnabled(timerRow.IsEnabled, timerRow.SourceTimer);
            _disciplineTimersWindow.EnabledChangedForTimer(timerRow.IsEnabled, timerRow.SourceTimer.Id);
            _alertTimersWindow.EnabledChangedForTimer(timerRow.IsEnabled, timerRow.SourceTimer.Id);
        }
        private void UpdateRowColors()
        {
            for (var i = 0; i < TimerRows.Count; i++)
            {
                if (i % 2 == 1)
                {
                    TimerRows[i].RowBackground = Brushes.Gray;
                }
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
