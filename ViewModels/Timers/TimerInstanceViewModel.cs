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
        private TimeSpan _timerValue;
        private bool displayTimerValue;
        private int charges;
        private bool displayTimer;
        private double _hpTimerMonitor = 0;
        private double timerValue;

        public event Action<TimerInstanceViewModel> TimerExpired = delegate { };
        public event Action TimerTriggered = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<int> ChargesUpdated = delegate { };
        public int Charges
        {
            get => charges; set
            {
                charges = value;
                OnPropertyChanged();
                OnPropertyChanged("ShowCharges");
                ChargesUpdated(Charges);
            }
        }
        public DateTime StartTime { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool ShowCharges => Charges > 1;
        public Timer SourceTimer { get; set; } = new Timer();
        public double CurrentMonitoredHP { get; set; }
        public string TargetAddendem { get; set; }
        public long TargetId { get; set; }
        public string TimerName => GetTimerName();
        public double MaxTimerValue { get; set; }
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public double TimerValue
        {
            get => timerValue; set
            {
                timerValue = value;

                if (MaxTimerValue == 0 || TimerValue < 0 || TimerValue > MaxTimerValue)
                {
                    OnPropertyChanged("RemainderWidth");
                    OnPropertyChanged("BarWidth");
                    return; 
                }
                RemainderWidth = GetRemainderWidth();
                BarWidth = GetBarWidth();
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
            }
        }
        public bool DisplayTimer
        {
            get => displayTimer; set
            {
                displayTimer = value;
                OnPropertyChanged();
            }
        }
        public bool DisplayTimerValue
        {
            get => displayTimerValue; set
            {
                displayTimerValue = value;
                OnPropertyChanged();
            }
        }


        public GridLength RemainderWidth { get; set; } = new GridLength(0, GridUnitType.Star);

        private GridLength GetRemainderWidth()
        {
            
            return (SourceTimer.IsAlert ? new GridLength(0, GridUnitType.Star) : new GridLength(1 - (TimerValue / MaxTimerValue), GridUnitType.Star));
        }

        public GridLength BarWidth { get; set; } = new GridLength(1, GridUnitType.Star);

        private GridLength GetBarWidth()
        {
            return (SourceTimer.IsAlert ? new GridLength(1, GridUnitType.Star) : new GridLength(TimerValue / MaxTimerValue, GridUnitType.Star));
        }
        public TimerInstanceViewModel(Timer swtorTimer)
        {
            SourceTimer = swtorTimer;
            MaxTimerValue = swtorTimer.DurationSec;

            _dtimer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher);
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
            MaxTimerValue = _timerValue.TotalSeconds;
            TimerValue = _timerValue.TotalSeconds;
        }

        private void ClearAlert(object sender, EventArgs args)
        {
            Complete();
        }
        public void Reset(DateTime timeStampOfReset)
        {
            var offset = (DateTime.Now - timeStampOfReset).TotalSeconds * -1;
            StartTime = timeStampOfReset;
            _timerValue = TimeSpan.FromSeconds(MaxTimerValue + offset);
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void TriggerTimeTimer(DateTime timeStampWhenTrigged)
        {
            if (!SourceTimer.IsAlert)
            {
                if (SourceTimer.HideUntilSec == 0)
                {
                    DisplayTimer = true;
                }
                DisplayTimerValue = true;
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
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");

            _dtimer.IsEnabled = true;
            _dtimer.Start();
            StartTime = DateTime.Now;
            LastUpdate = StartTime;
            TimerTriggered();
        }
        public void TriggerHPTimer(double currentHP)
        {
            DisplayTimer = true;
            DisplayTimerValue = true;
            MaxTimerValue = 100d;
            CurrentMonitoredHP = currentHP;
            _hpTimerMonitor = currentHP;
            TimerValue = SourceTimer.HPPercentage;
            _dtimer.Tick += UpdateHP;
            _dtimer.Start();
            TimerTriggered();
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void Tick(object sender, EventArgs args)
        {
            _timerValue = _timerValue.Add(-1*(DateTime.Now - LastUpdate));
            TimerValue = _timerValue.TotalSeconds;
            if (SourceTimer.HideUntilSec > 0 && !DisplayTimer && TimerValue <= SourceTimer.HideUntilSec)
                DisplayTimer = true;

            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            LastUpdate = DateTime.Now;
            if (TimerValue <= 0)
                Complete();
        }
        public void UpdateHP(object sender, EventArgs args)
        {
            _hpTimerMonitor = CurrentMonitoredHP;
            if (_hpTimerMonitor <= SourceTimer.HPPercentage)
                Complete();
        }
        public void Complete()
        {
            _dtimer?.Stop();
            _dtimer.IsEnabled = false;
            TimerExpired(this);
        }
        private string GetTimerName()
        {
            var name = "";
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
            {
                name = SourceTimer.Name + ": " + SourceTimer.HPPercentage.ToString("N2") + "%";
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
            _dtimer.IsEnabled = false;
        }
    }
}
