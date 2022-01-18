using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Timers
{
    public class TimerInstance
    {
        private bool _isCancelled;
        private TimerInstance expirationTimer;

        public event Action<TimerInstanceViewModel> NewTimerInstance = delegate { };
        public event Action<TimerInstanceViewModel> TimerOfTypeExpired = delegate { };
        public Timer SourceTimer;
        public List<TimerInstanceViewModel> ActiveTimerInstancesForTimer = new List<TimerInstanceViewModel>();
        public bool TrackOutsideOfCombat { get; set; }
        public bool IsEnabled { get; set; }
        public int RepeatTimes { get; set; }
        public string ExperiationTimerId { get; set; }
        public TimerInstance ExpirationTimer
        {
            get => expirationTimer; set
            {
                expirationTimer = value;
                expirationTimer.TimerOfTypeExpired += _ => CreateTimerInstance(DateTime.Now);
            }
        }
        public TimerInstance(Timer sourceTimer)
        {
            SourceTimer = sourceTimer;
            ExperiationTimerId = sourceTimer.ExperiationTimerId;
            IsEnabled = sourceTimer.IsEnabled;
            TrackOutsideOfCombat = sourceTimer.TrackOutsideOfCombat;
            RepeatTimes = sourceTimer.Repeats;
        }
        public void Cancel(TimerInstanceViewModel timer = null)
        {
            if (timer == null)
            {
                ActiveTimerInstancesForTimer.ForEach(t => t.Dispose());
                ActiveTimerInstancesForTimer.ForEach(t => TimerOfTypeExpired(t));
                ActiveTimerInstancesForTimer.Clear();
            }
            else
            {
                ActiveTimerInstancesForTimer.Remove(timer);
                TimerOfTypeExpired(timer);
                timer.Dispose();
            }
            
            _isCancelled = true;
            RepeatTimes = 0;
        }
        public void UnCancel()
        {
            _isCancelled = false;
        }
        public void Reset(TimerInstanceViewModel timer)
        {
            TimerOfTypeExpired(timer);
            if (SourceTimer.IsPeriodic && RepeatTimes <= SourceTimer.Repeats)
            {
                RepeatTimes++;
                timer.Trigger(DateTime.Now);
            }
            if (RepeatTimes > SourceTimer.Repeats || SourceTimer.TriggerType == TimerKeyType.FightDuration)
                Cancel(timer);
            else
            {
                ActiveTimerInstancesForTimer.Remove(timer);
                TimerOfTypeExpired(timer);
                timer.Dispose();
            }
        }
        internal void CheckForTrigger(ParsedLogEntry log, DateTime startTime)
        {
            var wasTriggered = false;
            if (!IsEnabled)
                return;
            if (_isCancelled && !TrackOutsideOfCombat)
                return;
            var targetAdendum = "";
            long targetId = 0;
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.CombatStart:
                    wasTriggered = TriggerDetection.CheckForComabatStart(log);
                    break;
                case TimerKeyType.AbilityUsed:
                    wasTriggered = TriggerDetection.CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.EffectGained:
                    wasTriggered = TriggerDetection.CheckForEffectGain(log, SourceTimer.Effect, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.EffectLost:
                    wasTriggered = TriggerDetection.CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.EntityHP:
                    wasTriggered = TriggerDetection.CheckForHP(log, SourceTimer.HPPercentage, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.FightDuration:
                    wasTriggered = TriggerDetection.CheckForFightDuration(log, SourceTimer.CombatTimeElapsed, startTime);
                    break;
            }
            if (wasTriggered && ActiveTimerInstancesForTimer.Any(t => t.TargetId == targetId))
            {
                var timerToRestart = ActiveTimerInstancesForTimer.First(t => t.TargetId == targetId);
                timerToRestart.Reset();
            }
            if (wasTriggered && !ActiveTimerInstancesForTimer.Any(t=>t.TargetId == targetId))
            {
                CreateTimerInstance(log.TimeStamp,targetAdendum, targetId);
            }

        }
        private void CreateTimerInstance(DateTime timeStamp, string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += Reset;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            ActiveTimerInstancesForTimer.Add(timerVM);
            NewTimerInstance(timerVM);
            timerVM.Trigger(timeStamp);
        }
    }
}
