using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimersCreationViewModel : INotifyPropertyChanged
    {
        private TimersWindowViewModel _timersWindowVM;
        private string selectedTimerSourceType;
        private bool _isLocked;
        private Timer _timerEdited;
        private bool timersActive;

        private List<DefaultTimersData> _savedTimersData = new List<DefaultTimersData>();

        public event PropertyChangedEventHandler PropertyChanged;

        public List<string> TimerSourcesTypes = new List<string> { "Encounters", "Disciplines" };
        private string selectedTimerSource;

        public string SelectedTimerSourceType
        {
            get => selectedTimerSourceType; set
            {
                selectedTimerSourceType=value;
                if (selectedTimerSourceType ==  "Encounters")
                    AvailableTimerSources = EncounterTimersList;
                if (selectedTimerSourceType ==  "Disciplines")
                    AvailableTimerSources = DisciplineTimersList;
                OnPropertyChanged("AvailableTimerSources");
            }
        }

        public List<string> AvailableTimerSources { get; set; }
        public string SelectedTimerSource
        {
            get => selectedTimerSource; set
            {
                selectedTimerSource=value;
                OnPropertyChanged();
                UpdateTimerRows();
            }
        }
        public List<string> EncounterTimersList { get; set; } = new List<string>();
        public List<string> DisciplineTimersList { get; set; } = new List<string>();

        public bool TimersActive
        {
            get => timersActive; set
            {
                timersActive = value;
                if (timersActive)
                {
                    _timersWindowVM.SetPlayer(SelectedTimerSource);
                    _timersWindowVM.ShowTimers(_isLocked);
                }
                else
                    _timersWindowVM.HideTimers();
                OnPropertyChanged();
            }
        }
        public void HideTimers()
        {
            _timersWindowVM.HideTimers();
        }
        public ObservableCollection<TimerRowInstanceViewModel> TimerRows { get; set; } = new ObservableCollection<TimerRowInstanceViewModel>();

        public TimersCreationViewModel()
        {
            _timersWindowVM = new TimersWindowViewModel();
            CombatLogStateBuilder.PlayerDiciplineChanged += SetClass;
            RefreshAvaialbleTriggerOwners();
            if (DisciplineTimersList.Count > 0)
            {
                SelectedTimerSource = DisciplineTimersList[0];
                OnPropertyChanged("SelectedTimerSource");
            }

        }

        private void RefreshAvaialbleTriggerOwners()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            if (!_savedTimersData.Any(t => t.TimerSource == "Shared"))
            {
                _savedTimersData.Add(new DefaultTimersData() { TimerSource = "Shared"});
                DefaultTimersManager.SetSavedTimers(new List<Timer>(), "Shared");
            }
            DisciplineTimersList = new List<string>(_savedTimersData.Where(t=>!EncounterTimersList.Contains(t.TimerSource)).Select(t=>t.TimerSource));
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
            _timersWindowVM.RefreshTimers();
            UpdateRowColors();
        }
        private void SaveNewTimer(Timer timer)
        {
            DefaultTimersManager.AddTimerForCharacter(timer, SelectedTimerSource);

        }
        public void UpdateLock(bool state)
        {
            _timersWindowVM.UpdateLock(state);
            _isLocked = state;
        }
        public void SetClass(Entity player, SWTORClass swtorclass)
        {
            if (!player.IsLocalPlayer || !CombatMonitorViewModel.IsLiveParseActive())
                return;
            _timersWindowVM.SetPlayer(player.Name, swtorclass);
            SelectedTimerSource = player.Name + " " + swtorclass.Discipline;
            RefreshAvaialbleTriggerOwners();
        }
        private void UpdateTimerRows()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            var timers = _savedTimersData.First(t=>t.TimerSource == SelectedTimerSource);
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
            _timersWindowVM.RefreshTimers();
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
            _timersWindowVM.EnabledChangedForTimer(timerRow.IsEnabled, timerRow.SourceTimer.Id);
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
