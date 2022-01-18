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
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class TimerInstanceViewModel : INotifyPropertyChanged, IDisposable
    {
        private System.Timers.Timer _timer;

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
        public TimerInstanceViewModel(Timer swtorTimer)
        {
            SourceTimer = swtorTimer;
            if (!swtorTimer.IsAlert)
            {
                TimerValue = DurationSec;
                _timer = new System.Timers.Timer(50);
                _timer.Elapsed += Tick;
            }
            else
            {
                _timer = new System.Timers.Timer(3000);
                _timer.Elapsed += ClearAlert;
            }
        }

        private void ClearAlert(object sender, ElapsedEventArgs e)
        {
            Complete();
        }
        public void Reset()
        {
            TimerValue = DurationSec;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void Trigger(DateTime timeStampWhenTrigged)
        {
            var offset = (DateTime.Now - timeStampWhenTrigged).TotalSeconds;
            TimerValue -= offset;
            TimerTriggered();
            OnPropertyChanged("IsTriggered");
            _timer.Start();
        }
        public void Tick(object sender, ElapsedEventArgs e)
        {
            TimerValue -= 0.050;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            if (TimerValue <= 0)
                Complete();
        }
        public void Complete()
        {
            _timer.Stop();
            TimerExpired(this);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            _timer.Stop();
        }
    }
}
