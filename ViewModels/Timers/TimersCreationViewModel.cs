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
        private string selectedPlayer;
        private Dictionary<string, DefaultTimersData> _savedTimersData = new Dictionary<string, DefaultTimersData>();

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<string> SavedPlayerNames { get; set; } = new ObservableCollection<string>();
        public string SelectedPlayer
        {
            get => selectedPlayer; set
            {
                selectedPlayer = value;
                UpdateTimerRows();
            }
        }
        public ObservableCollection<TimerRowInstanceViewModel> TimerRows { get; set; } = new ObservableCollection<TimerRowInstanceViewModel>();

        public TimersCreationViewModel()
        {
            _timersWindowVM = new TimersWindowViewModel();
            CombatLogStateBuilder.PlayerDiciplineChanged += SetClass;
            RefreshAvaialbleTriggerOwners();
            if (SavedPlayerNames.Count > 0)
            {
                SelectedPlayer = SavedPlayerNames[0];
                OnPropertyChanged("SelectedPlayer");
            }

        }

        private void RefreshAvaialbleTriggerOwners()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            if (!_savedTimersData.ContainsKey("Shared"))
            {
                _savedTimersData["Shared"] = new DefaultTimersData();
                DefaultTimersManager.SetSavedTimers(new List<Timer>(), "Shared");
            }
            SavedPlayerNames = new ObservableCollection<string>(_savedTimersData.Keys.OrderBy(d => d));
            OnPropertyChanged("SavedPlayerNames");
        }

        public ICommand CreateNewTimerCommand => new CommandHandler(CreateNewTimer);

        private void CreateNewTimer(object obj)
        {
            var vm = new ModifyTimerViewModel(SelectedPlayer);
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
                DefaultTimersManager.RemoveTimerForCharacter(_timerEdited, SelectedPlayer);
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
            DefaultTimersManager.AddTimerForCharacter(timer, SelectedPlayer);

        }
        public void UpdateLock(bool state)
        {
            _timersWindowVM.UpdateLock(state);
        }
        public void SetClass(Entity player, SWTORClass swtorclass)
        {
            if (!player.IsLocalPlayer)
                return;
            _timersWindowVM.SetPlayer(player.Name, swtorclass);
            RefreshAvaialbleTriggerOwners();
        }
        private void UpdateTimerRows()
        {
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            var timers = _savedTimersData[selectedPlayer];
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
            DefaultTimersManager.RemoveTimerForCharacter(obj.SourceTimer, SelectedPlayer);
            TimerRows.Remove(obj);
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
            DefaultTimersManager.SetIdForTimer(obj.SourceTimer, SelectedPlayer, id);
            TimerDatabaseAccess.AddTimer(obj.SourceTimer);
        }
        private Timer _timerEdited;
        private void Edit(TimerRowInstanceViewModel obj)
        {
            _timerEdited = obj.SourceTimer;
            TimerRows.Remove(obj);
            var vm = new ModifyTimerViewModel(SelectedPlayer);
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
                    TimerRows[i].RowBackground = Brushes.WhiteSmoke;
                }
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
