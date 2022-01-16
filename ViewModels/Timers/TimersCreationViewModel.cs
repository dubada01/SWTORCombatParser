using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

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
            _savedTimersData = DefaultTimersManager.GetAllDefaults();
            if (!_savedTimersData.ContainsKey("Shared"))
            {
                _savedTimersData["Shared"] = new DefaultTimersData();
                DefaultTimersManager.SetSavedTimers(new List<Timer>(), "Shared");
            }
            SavedPlayerNames = new ObservableCollection<string>(_savedTimersData.Keys.OrderBy(d => d));
            OnPropertyChanged("SavedPlayerNames");
            if (SavedPlayerNames.Count > 0)
            {
                SelectedPlayer = SavedPlayerNames[0];
                OnPropertyChanged("SelectedPlayer");
            }

        }
        public ICommand CreateNewTimerCommand => new CommandHandler(CreateNewTimer);

        private void CreateNewTimer(object obj)
        {
            var vm = new ModifyTimerViewModel();
            vm.OnNewTimer += NewTimer;
            var t = new TimerModificationWindow(vm);
            t.Show();
        }

        private void CancelEdit()
        {
            var addedBack = new TimerRowInstanceViewModel() { SourceTimer = _timerEdited };
            addedBack.EditRequested += Edit;
            addedBack.ShareRequested += Share;
            addedBack.DeleteRequested += Delete;
            TimerRows.Add(addedBack);
        }

        private void NewTimer(Timer obj, bool wasEdit)
        {
            if (wasEdit)
                DefaultTimersManager.RemoveTimerForCharacter(_timerEdited, SelectedPlayer);
            SaveNewTimer(obj);
            var newTimer = new TimerRowInstanceViewModel() { SourceTimer = obj };
            newTimer.EditRequested += Edit;
            newTimer.ShareRequested += Share;
            newTimer.DeleteRequested += Delete;
            TimerRows.Add(newTimer);
        }
        private void SaveNewTimer(Timer timer)
        {
            DefaultTimersManager.AddTimerForCharacter(timer, SelectedPlayer);
        }
        public void UpdateLock(bool state)
        {
            _timersWindowVM.UpdateLock(state);
        }
        public void SetPlayer(string player)
        {
            _timersWindowVM.SetPlayer(player);
        }
        private void UpdateTimerRows()
        {
            var timers = _savedTimersData[selectedPlayer];
            var timerObjects = timers.Timers.Select(t => new TimerRowInstanceViewModel() { SourceTimer = t }).ToList();
            timerObjects.ForEach(t => t.EditRequested += Edit);
            timerObjects.ForEach(t => t.ShareRequested += Share);
            timerObjects.ForEach(t => t.DeleteRequested += Delete);
            TimerRows = new ObservableCollection<TimerRowInstanceViewModel>(timerObjects);
            OnPropertyChanged("TimerRows");
        }

        private void Delete(TimerRowInstanceViewModel obj)
        {
            DefaultTimersManager.RemoveTimerForCharacter(obj.SourceTimer, SelectedPlayer);
            TimerRows.Remove(obj);
        }

        private void Share(TimerRowInstanceViewModel obj)
        {
            var currentIds = TimerDatabaseAccess.GetAllTimerIds();
            string id;
            do
            {
                id = AlphanumericsGenerator.RandomString(3);
            } while (currentIds.Contains(id));
            
        }
        private Timer _timerEdited;
        private void Edit(TimerRowInstanceViewModel obj)
        {
            _timerEdited = obj.SourceTimer;
            TimerRows.Remove(obj);
            var vm = new ModifyTimerViewModel();
            vm.OnNewTimer += NewTimer;
            vm.OnCancelEdit += CancelEdit;
            var t = new TimerModificationWindow(vm);
            t.Show();
            vm.Edit(_timerEdited.Copy());
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
