using SWTORCombatParser.DataStructures;
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
        public event Action<TimerRowInstanceViewModel> EditRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> ShareRequested = delegate { };
        public event Action<TimerRowInstanceViewModel> DeleteRequested = delegate { };
        public Timer SourceTimer { get; set; } = new Timer();

        public bool IsEnabled { get { return SourceTimer.IsEnabled; } set { SourceTimer.IsEnabled = value; } }
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
    public class TimerInstanceViewModel:INotifyPropertyChanged
    {
        private System.Timers.Timer _timer;
        private bool _isCancelled;

        public event Action<Timer> TimerExpired = delegate { };
        public event Action TimerTriggered = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public Timer SourceTimer { get; set; } = new Timer();
        public bool IsEnabled => SourceTimer.IsEnabled;
        public string TimerName => SourceTimer.Name;
        public double DurationSec => SourceTimer.DurationSec;
        public Color TimerColor => SourceTimer.TimerColor;
        public SolidColorBrush TimerBackground => new SolidColorBrush(TimerColor);

        public bool IsTriggered { get; set; }
        public double TimerValue { get; set; }

        public GridLength RemainderWidth => GetRemainderWidth();

        private GridLength GetRemainderWidth()
        {
            return IsTriggered ? new GridLength(1-(TimerValue / DurationSec), GridUnitType.Star) : new GridLength(1,GridUnitType.Star);
        }

        public GridLength BarWidth => GetBarWidth();

        private GridLength GetBarWidth()
        {
            return IsTriggered ? new GridLength(TimerValue / DurationSec, GridUnitType.Star) : new GridLength();
        }
        public TimerInstanceViewModel(Timer swtorTimer)
        {
            SourceTimer = swtorTimer;
            TimerValue = DurationSec;
            _timer = new System.Timers.Timer(50);
            CompositionTarget.Rendering += OnRender;
            _timer.Elapsed += Tick;
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
        }
        public void Trigger()
        {
            if (!IsEnabled)
                return;
            TimerTriggered();
            IsTriggered = true;
            _timer.Start();
            OnPropertyChanged("IsTriggered");
        }
        public void Tick(object sender, ElapsedEventArgs e)
        {
            if (!IsTriggered)
                return;
            TimerValue -= 0.050;
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
            if (SourceTimer.IsPeriodic && !_isCancelled)
                Trigger();
            if (_isCancelled)
                _isCancelled = false;
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
                    wasTriggered=TriggerDetection.CheckForComabatStart(log);
                    break;
                case TimerKeyType.AbilityUsed:
                    wasTriggered=TriggerDetection.CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.EffectGained:
                    wasTriggered=TriggerDetection.CheckForEffectGain(log, SourceTimer.Effect, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.EffectLost:
                    wasTriggered=TriggerDetection.CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    break;
                case TimerKeyType.EntityHP:
                    wasTriggered=TriggerDetection.CheckForHP(log, SourceTimer.HPPercentage, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    break;
                //case TimerKeyType.TimerExpired:
                //    wasTriggered=TriggerDetection.CheckForTimerExperiation(log, SourceTimer.ExperiationTrigger);
                //    break;
            }
            if (wasTriggered)
                Trigger();
        }


    }
}
