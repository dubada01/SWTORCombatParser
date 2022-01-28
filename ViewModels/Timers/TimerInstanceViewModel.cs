using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerInstanceViewModel : INotifyPropertyChanged, IDisposable
    {
        private DispatcherTimer _dtimer;

        public event Action<TimerInstanceViewModel> TimerExpired = delegate { };
        public event Action TimerTriggered = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public Timer SourceTimer { get; set; } = new Timer();
        public double CurrentMonitoredHP { get; set; }
        public string TargetAddendem { get; set; }
        public long TargetId { get; set; }
        public string TimerName => GetTimerName();
        public double MaxTimerValue { get; set; }
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public double TimerValue { get; set; }
        public bool DisplayTimerValue
        {
            get => displayTimerValue; set
            {
                displayTimerValue = value;
                OnPropertyChanged();
            }
        }


        public GridLength RemainderWidth => GetRemainderWidth();

        private GridLength GetRemainderWidth()
        {
            return (SourceTimer.IsAlert ? new GridLength(0, GridUnitType.Star) : new GridLength(1 - (TimerValue / MaxTimerValue), GridUnitType.Star));
        }

        public GridLength BarWidth => GetBarWidth();

        private GridLength GetBarWidth()
        {
            return (SourceTimer.IsAlert ? new GridLength(1, GridUnitType.Star) : new GridLength(TimerValue / MaxTimerValue, GridUnitType.Star));
        }
        private TimeSpan _timerValue;
        private bool displayTimerValue;

        public TimerInstanceViewModel(Timer swtorTimer)
        {
            SourceTimer = swtorTimer;

            _dtimer = new DispatcherTimer(DispatcherPriority.Send, Application.Current.Dispatcher);
            _dtimer.IsEnabled = true;
            if (!swtorTimer.IsAlert)
            {
                _timerValue = TimeSpan.FromSeconds(MaxTimerValue);
                _dtimer.Interval = TimeSpan.FromMilliseconds(100);
            }
            else
            {
                _timerValue = TimeSpan.FromSeconds(3);
                _dtimer.Interval = TimeSpan.FromSeconds(3);
            }
            TimerValue = _timerValue.TotalSeconds;
        }

        private void ClearAlert(object sender, EventArgs args)
        {
            Complete();
        }
        public void Reset(DateTime timeStampOfReset)
        {
            var offset = (DateTime.Now - timeStampOfReset).TotalSeconds * -1;
            _timerValue = TimeSpan.FromSeconds(MaxTimerValue + offset);
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void Trigger(DateTime timeStampWhenTrigged)
        {
            if (!SourceTimer.IsAlert)
            {
                DisplayTimerValue = true;
                MaxTimerValue = SourceTimer.DurationSec;
                var offset = (DateTime.Now - timeStampWhenTrigged).TotalSeconds * -1;
                _timerValue = TimeSpan.FromSeconds(MaxTimerValue + offset);
                TimerValue = _timerValue.TotalSeconds;
                _dtimer.Tick += Tick;
            }
            else
            {
                DisplayTimerValue = false;
                _dtimer.Tick += ClearAlert;
            }

            _dtimer.Start();
            TimerTriggered();
        }
        public void Trigger(double currentHP)
        {
            MaxTimerValue = 100d;
            CurrentMonitoredHP = currentHP;
            TimerValue = CurrentMonitoredHP;
            _dtimer.Tick += UpdateHP;
            _dtimer.Start();
            TimerTriggered();
        }
        public void Tick(object sender, EventArgs args)
        {
            _timerValue = _timerValue.Add(TimeSpan.FromMilliseconds(-100));
            TimerValue = _timerValue.TotalSeconds;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            if (TimerValue <= 0)
                Complete();
        }
        public void UpdateHP(object sender, EventArgs args)
        {
            TimerValue = CurrentMonitoredHP;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            if (TimerValue <= SourceTimer.HPPercentage)
                Complete();
        }
        public void Complete()
        {
            _dtimer?.Stop();
            TimerExpired(this);
        }
        private string GetTimerName()
        {
            var name = "";
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
            {
                name = SourceTimer.Name + ": " + SourceTimer.HPPercentage + "%";
            }
            else
            {
                if (SourceTimer.IsAlert)
                {
                    name = "Alert! " + SourceTimer.Name;
                }
                else
                {
                    name = SourceTimer.Name + (string.IsNullOrEmpty(TargetAddendem) ? "" : " on ") + TargetAddendem;
                }
            }

            return name;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            _dtimer?.Stop();
        }
    }
}
