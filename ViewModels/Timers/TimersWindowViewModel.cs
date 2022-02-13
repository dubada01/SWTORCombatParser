using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimersWindowViewModel:INotifyPropertyChanged
    {
        private string _currentPlayer;
        private TimersWindow _timerWindow;
        private bool _timersEnabled;
        private List<TimerInstance> _createdTimers = new List<TimerInstance>();

        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<string> OnCharacterDetected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool OverlaysMoveable { get; set; } = true;
        public ObservableCollection<TimerInstanceViewModel> SwtorTimers { get; set; } = new ObservableCollection<TimerInstanceViewModel>();
        
        public string TimerTitle { get; set; }
        public TimersWindowViewModel()
        {
            CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
            CombatLogStreamer.CombatUpdated += NewInCombatLogs;
            CombatLogStreamer.NewLineStreamed += NewLogInANDOutOfCombat;
            _timerWindow = new TimersWindow(this);
        }
        public void ShowTimers(bool isLocked)
        {
            App.Current.Dispatcher.Invoke(() => {
                _timerWindow.Show();
                UpdateLock(isLocked);
            });
       
        }
        public void HideTimers()
        {
            App.Current.Dispatcher.Invoke(() => {
                _timerWindow.Hide();
            });
        }
        public void SetPlayer(string playerText)
        {
            _currentPlayer = playerText;
            UpdatePlayer();
        }
        public void SetPlayer(string player, SWTORClass swtorclass)
        {
            _currentPlayer = player + " " + swtorclass.Discipline;
            UpdatePlayer();
        }
        private void UpdatePlayer()
        {
            TimerTitle = _currentPlayer + " Timers";
            OnPropertyChanged("TimerTitle");
            SwtorTimers = new ObservableCollection<TimerInstanceViewModel>();
            _timerWindow.SetPlayer(_currentPlayer);
            RefreshTimers();
            App.Current.Dispatcher.Invoke(() => {
                var defaultTimersInfo = DefaultTimersManager.GetDefaults(_currentPlayer);
                _timerWindow.Top = defaultTimersInfo.Position.Y;
                _timerWindow.Left = defaultTimersInfo.Position.X;
                _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;

                ShowTimers(!OverlaysMoveable);
            });
        }
        public void RefreshTimers()
        {
            if (string.IsNullOrEmpty(_currentPlayer))
                return;
            _createdTimers.ForEach(t => t.TimerOfTypeExpired -= RefreshTimerVisuals);
            _createdTimers.ForEach(t => t.NewTimerInstance -= AddTimerVisual);
            var defaultTimersInfo = DefaultTimersManager.GetDefaults(_currentPlayer);
            var sharedTimers = DefaultTimersManager.GetDefaults("Shared").Timers;
            var timers = defaultTimersInfo.Timers;
            var allTimers = timers.Concat(sharedTimers);
            var timerInstances = allTimers.Select(t => new TimerInstance(t)).ToList();
            foreach(var timerInstance in timerInstances)
            {
                if (!string.IsNullOrEmpty(timerInstance.ExperiationTimerId))
                {
                    var trigger = timerInstances.First(t => t.SourceTimer.Id == timerInstance.ExperiationTimerId);
                    timerInstance.ExpirationTimer = trigger;
                }
            }
            timerInstances.ForEach(t => t.TimerOfTypeExpired += RefreshTimerVisuals);
            timerInstances.ForEach(t => t.NewTimerInstance += AddTimerVisual);
            _createdTimers = new List<TimerInstance>(timerInstances);
        }

        private void AddTimerVisual(TimerInstanceViewModel obj)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                SwtorTimers.Add(obj);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(SwtorTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
            });
        }

        private void NewLogInANDOutOfCombat(ParsedLogEntry log)
        {
            var validTimers = _createdTimers.Where(t => t.TrackOutsideOfCombat && CheckEncounterAndBoss(t,log));
                Parallel.ForEach(validTimers, timer => {
                    timer.CheckForTrigger(log, DateTime.Now);
                });
        }

        private bool CheckEncounterAndBoss(TimerInstance t, ParsedLogEntry log)
        {
            var timerEncounter = t.SourceTimer.SpecificEncounter;
            var timerBoss = t.SourceTimer.SpecificBoss;
            if (timerEncounter == "All")
                return true;
            
            var parentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp);
            if (parentEncounter.Name == timerEncounter)
                return true;
            return false;
        }

        private void NewInCombatLogs(CombatStatusUpdate obj)
        {
            if(obj.Type == UpdateType.Start)
            {
                UncancellBeforeCombat();
            }
            if(obj.Type == UpdateType.Stop)
            {
                CancelAfterCombat();
            }
            if (obj.Logs == null || !_timersEnabled || obj.Type == UpdateType.Stop)
                return;
            var logs = obj.Logs;
            foreach (var log in logs)
            {
                var validTimers = _createdTimers.Where(t => !t.TrackOutsideOfCombat && CheckEncounterAndBoss(t, log));
                Parallel.ForEach(validTimers, timer => {
                    timer.CheckForTrigger(log, obj.CombatStartTime);
                });
            }
        }
        private void UncancellBeforeCombat()
        {
            foreach (var timer in _createdTimers)
            {
                timer.UnCancel();
            }
        }
        private void CancelAfterCombat()
        {
            foreach (var timer in _createdTimers)
            {
                timer.Cancel();
            }
        }
        private void RefreshTimerVisuals(TimerInstanceViewModel removedTimer)
        {
            App.Current.Dispatcher.Invoke(() => {
                SwtorTimers.Remove(removedTimer);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(SwtorTimers.OrderBy(t => t.TimerValue));
                OnPropertyChanged("SwtorTimers");
            });

        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private void EnableTimers()
        {
            _timersEnabled = true;
        }
        internal void UpdateLock(bool value)
        {
            OverlaysMoveable = !value;
            OnPropertyChanged("OverlaysMoveable");
            OnLocking(value);
        }

        internal void EnabledChangedForTimer(bool isEnabled, string id)
        {
            var timerToUpdate = _createdTimers.FirstOrDefault(t => t.SourceTimer.Id == id);
            if (timerToUpdate == null)
                return;
            timerToUpdate.IsEnabled = isEnabled;
        }
    }
}
