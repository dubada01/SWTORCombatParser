using SWTORCombatParser.Model.CombatParsing;
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
    public class TimersWindowViewModel
    {
        private string _currentPlayer;
        private TimersWindow _timerWindow;
        private bool _timersEnabled;

        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<string> OnCharacterDetected = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public bool OverlaysMoveable { get; set; } = true;
        public ObservableCollection<TimerInstanceViewModel> Timers { get; set; } = new ObservableCollection<TimerInstanceViewModel>();
        public TimersWindowViewModel()
        {
            CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
            CombatLogStreamer.CombatUpdated += NewLogs;
            _timerWindow = new TimersWindow(this);
        }
        private void ShowTimers(object bla)
        {
            if (_timerWindow == null)
                return;
            _timerWindow.Show();
            //Timers.Add(new TimerInstanceViewModel(new Timer() { Name = "Test", DurationSec = 10, TriggerType = TimerKeyType.AbilityUsed, SourceIsLocal = true , Ability = "Shock"}));
            OnPropertyChanged("Timers");
            DefaultTimersManager.SetSavedTimers(Timers.Select(c => c.SourceTimer).ToList(), _currentPlayer);
        }
        public void SetPlayer(string player)
        {
            _currentPlayer = player;
            _timerWindow.SetPlayer(_currentPlayer);
            var defaultTimerWindow = DefaultTimersManager.GetDefaults(_currentPlayer);
            var timers = defaultTimerWindow.Timers.Select(t => new TimerInstanceViewModel(t)).ToList();
            timers.ForEach(t => t.TimerTriggered += OrderTimers);
            Timers = new ObservableCollection<TimerInstanceViewModel>(timers);
            _timerWindow.Top = defaultTimerWindow.Position.Y;
            _timerWindow.Left = defaultTimerWindow.Position.X;
            _timerWindow.Width = defaultTimerWindow.WidtHHeight.X;
            _timerWindow.Height = defaultTimerWindow.WidtHHeight.Y;
            ShowTimers(_currentPlayer);
        }
        private void NewLogs(CombatStatusUpdate obj)
        {
            if (obj.Logs == null || !_timersEnabled || obj.Type == UpdateType.Stop)
                return;
            var logs = obj.Logs;
            foreach (var log in logs)
            {
                foreach (var timer in Timers)
                {
                    timer.CheckForTrigger(log, obj.CombatStartTime);
                }
            }
        }

        private void OrderTimers()
        {
            Timers = new ObservableCollection<TimerInstanceViewModel>(Timers.OrderBy(t => t.TimerValue));
            OnPropertyChanged("Timers");
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
            OverlaysMoveable = value;
            OnLocking(value);
        }
    }
}
