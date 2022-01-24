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
        private System.Timers.Timer _timer;
        private DispatcherTimer _dtimer;

        public event Action<TimerInstanceViewModel> TimerExpired = delegate { };
        public event Action TimerTriggered = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public Timer SourceTimer { get; set; } = new Timer();
        public string TargetAddendem { get; set; }
        public long TargetId { get; set; }
        public string TimerName => (SourceTimer.IsAlert?"Alert! ":"") + SourceTimer.Name + (string.IsNullOrEmpty(TargetAddendem)?"":" on ")+ TargetAddendem;
        public double DurationSec => SourceTimer.DurationSec;
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public double TimerValue { get; set; }

        public GridLength RemainderWidth => GetRemainderWidth();

        private GridLength GetRemainderWidth()
        {
            return(SourceTimer.IsAlert ? new GridLength(0, GridUnitType.Star) : new GridLength(1-(TimerValue / DurationSec), GridUnitType.Star));
        }

        public GridLength BarWidth => GetBarWidth();

        private GridLength GetBarWidth()
        {
            return(SourceTimer.IsAlert ? new GridLength(1,GridUnitType.Star) : new GridLength(TimerValue / DurationSec, GridUnitType.Star));
        }
        private TimeSpan _timerValue;
        public TimerInstanceViewModel(Timer swtorTimer)
        {
            SourceTimer = swtorTimer;
            if (!swtorTimer.IsAlert)
            {
                _timerValue = TimeSpan.FromSeconds(DurationSec);
                TimerValue = _timerValue.TotalSeconds;
                _dtimer = new DispatcherTimer(DispatcherPriority.Send, Application.Current.Dispatcher);
                _dtimer.IsEnabled = true;
                _dtimer.Interval = TimeSpan.FromMilliseconds(100);
                _dtimer.Tick += Tick;
                _dtimer.Start();
            }
            else
            {
                _timer = new System.Timers.Timer(3000);
                _timer.Elapsed += ClearAlert;
                _timer.Start();
            }
        }

        private void ClearAlert(object sender, ElapsedEventArgs e)
        {
            Complete();
        }
        public void Reset(DateTime timeStampOfReset)
        {
            var offset = (DateTime.Now - timeStampOfReset).TotalSeconds * -1;
            _timerValue = TimeSpan.FromSeconds(DurationSec + offset);
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void Trigger(DateTime timeStampWhenTrigged)
        {
            var offset = (DateTime.Now - timeStampWhenTrigged).TotalSeconds * -1;
            _timerValue = TimeSpan.FromSeconds(DurationSec+offset);
            TimerValue = _timerValue.TotalSeconds;
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
        public void Complete()
        {
            _dtimer?.Stop();
            _timer?.Stop();
            TimerExpired(this);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            _dtimer?.Stop();
            _timer?.Stop();
        }
    }
}
