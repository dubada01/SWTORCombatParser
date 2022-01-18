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
    public class TimerRowInstanceViewModel
    {
        private bool _isEnabled;

        public event Action<TimerRowInstanceViewModel> EditRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ShareRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> DeleteRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ActiveChanged = delegate { };
        public Timer SourceTimer { get; set; } = new Timer();

        public bool IsEnabled
        {
            get
            {
                return _isEnabled;
            }
            set
            {
                _isEnabled = value;
                ActiveChanged(this);
            }
        }
        public string Name => SourceTimer.Name;
        public string Type => SourceTimer.TriggerType.ToString();
        public double DurationSec => SourceTimer.DurationSec;
        public SolidColorBrush RowBackground { get; set; }
        public SolidColorBrush TimerBackground => new SolidColorBrush(SourceTimer.TimerColor);
        public ICommand EditCommand => new CommandHandler(Edit);
        private void Edit(object t)
        {
            EditRequested(this);
        }
        public ICommand ShareCommand => new CommandHandler(Share);
        private void Share(object t)
        {
            ShareRequested(this);
        }
        public ICommand DeleteCommand => new CommandHandler(Delete);
        private void Delete(object t)
        {
            DeleteRequested(this);
        }
    }
    public class TimerInstanceViewModel : INotifyPropertyChanged
    {
        private System.Timers.Timer _timer;
        private bool _isCancelled;
        private TimerInstanceViewModel expirationTimer;

        public event Action<Timer> TimerExpired = delegate { };
        public event Action TimerTriggered = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public Timer SourceTimer { get; set; } = new Timer();
        public string ExperiationTimerId { get; set; }
        public TimerInstanceViewModel ExpirationTimer
        {
            get => expirationTimer; set
            {
                expirationTimer = value;
                expirationTimer.TimerExpired += m => Trigger(DateTime.Now);
            }
        }
        public bool TrackOutsideOfCombat { get; set; }
        public bool IsEnabled { get; set; }
        public int RepeatTimes { get; set; }
        public string TimerName => (SourceTimer.IsAlert?"Alert! ":"") + SourceTimer.Name;
        public double DurationSec => SourceTimer.DurationSec;
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public bool IsTriggered { get; set; }
        public double TimerValue { get; set; }

        public GridLength RemainderWidth => GetRemainderWidth();

        private GridLength GetRemainderWidth()
        {
            return IsTriggered ? (SourceTimer.IsAlert ? new GridLength(0, GridUnitType.Star) : new GridLength(1-(TimerValue / DurationSec), GridUnitType.Star)) : new GridLength(1, GridUnitType.Star);
        }

        public GridLength BarWidth => GetBarWidth();

        private GridLength GetBarWidth()
        {
            return IsTriggered ? (SourceTimer.IsAlert ? new GridLength(1,GridUnitType.Star) : new GridLength(TimerValue / DurationSec, GridUnitType.Star)) : new GridLength();
        }
        public TimerInstanceViewModel(Timer swtorTimer)
        {
            SourceTimer = swtorTimer;
            ExperiationTimerId = swtorTimer.ExperiationTimerId;
            //CompositionTarget.Rendering += OnRender;
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

        private void OnRender(object sender, EventArgs e)
        {
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
        }
        public void Cancel()
        {
            _isCancelled = true;
            RepeatTimes = 0;
        }
        public void UnCancel()
        {
            _isCancelled = false;
        }
        public void Trigger(DateTime timeStampWhenTrigged)
        {
            if ((!IsEnabled || _isCancelled) && !SourceTimer.TrackOutsideOfCombat)
                return;
            var offset = (DateTime.Now - timeStampWhenTrigged).TotalSeconds;
            TimerValue -= offset;
            Trace.WriteLine("Offset: "+offset);
            TimerTriggered();
            IsTriggered = true;
            OnPropertyChanged("IsTriggered");
            _timer.Start();
        }
        public void Tick(object sender, ElapsedEventArgs e)
        {
            if (!IsTriggered)
                return;
            TimerValue -= 0.050;
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            if (TimerValue <= 0)
                Complete();
        }
        public void Complete()
        {
            TimerExpired(SourceTimer);
            Reset();
        }
        public void Reset()
        {
            _timer.Stop();
            IsTriggered = false;
            TimerValue = DurationSec;
            OnPropertyChanged("IsTriggered");
            OnPropertyChanged("TimerValue");
            OnPropertyChanged("BarWidth");
            OnPropertyChanged("RemainderWidth");
            if (SourceTimer.IsPeriodic && RepeatTimes <= SourceTimer.Repeats)
            {
                RepeatTimes ++;
                Trigger(DateTime.Now);
            }
            if (RepeatTimes > SourceTimer.Repeats)
                Cancel();
            if (SourceTimer.TriggerType == TimerKeyType.FightDuration)
                Cancel();
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void CheckForTrigger(ParsedLogEntry log, DateTime startTime)
        {
            var wasTriggered = false;
            if (IsTriggered)
                return;
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.CombatStart:
                    wasTriggered = TriggerDetection.CheckForComabatStart(log);
                    break;
                case TimerKeyType.AbilityUsed:
                    wasTriggered = TriggerDetection.CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.EffectGained:
                    wasTriggered = TriggerDetection.CheckForEffectGain(log, SourceTimer.Effect, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.EffectLost:
                    wasTriggered = TriggerDetection.CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.EntityHP:
                    wasTriggered = TriggerDetection.CheckForHP(log, SourceTimer.HPPercentage, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.FightDuration:
                    wasTriggered = TriggerDetection.CheckForFightDuration(log, SourceTimer.CombatTimeElapsed, startTime);
                    break;
            }
            if (wasTriggered)
                Trigger(log.TimeStamp);
        }


    }
}
