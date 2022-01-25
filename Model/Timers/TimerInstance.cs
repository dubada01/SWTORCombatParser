using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
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
            if (SourceTimer.IsPeriodic && (RepeatTimes <= SourceTimer.Repeats || SourceTimer.Repeats == 0))
            {
                RepeatTimes++;
                timer.Trigger(DateTime.Now);
            }
            if ((RepeatTimes > SourceTimer.Repeats && SourceTimer.Repeats != 0) || SourceTimer.TriggerType == TimerKeyType.FightDuration)
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
            TriggerType wasTriggered = TriggerType.None;
            if (!IsEnabled)
                return;
            if (_isCancelled && !TrackOutsideOfCombat)
                return;
            var targetAdendum = "";
            long targetId = 0;
            double currentHP = 100;
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
                    wasTriggered = TriggerDetection.CheckForEffectGain(log, SourceTimer.Effect, SourceTimer.AbilitiesThatRefresh, SourceTimer.Source, SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal);
                    if (wasTriggered == TriggerType.Refresh)
                    {
                        var currentTarget = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp);
                        targetAdendum = currentTarget.Name;
                        targetId = currentTarget.Id;
                    }
                    else
                    {
                        targetAdendum = log.Target.Name;
                        targetId = log.Target.Id;
                    }
                    break;
                case TimerKeyType.EffectLost:
                    wasTriggered = TriggerDetection.CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.EntityHP:
                    wasTriggered = TriggerDetection.CheckForHP(log, SourceTimer.HPPercentage, SourceTimer.HPPercentageDisplayBuffer, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    if(wasTriggered != TriggerType.None)
                    {
                        var entity = TriggerDetection.GetTargetId(log, SourceTimer.Target, SourceTimer.TargetIsLocal);
                        targetAdendum = entity.Name;
                        targetId = entity.Id;
                    }
                    if (wasTriggered == TriggerType.Start)
                    {
                        currentHP = TriggerDetection.GetCurrentTargetHPPercent(log,targetId);
                        var hpTimer = ActiveTimerInstancesForTimer.FirstOrDefault(t => t.SourceTimer.TriggerType == TimerKeyType.EntityHP && t.TargetId == targetId);
                        if (hpTimer != null)
                        {
                            hpTimer.CurrentMonitoredHP = currentHP;
                        }
                    }
                    break;
                case TimerKeyType.FightDuration:
                    wasTriggered = TriggerDetection.CheckForFightDuration(log, SourceTimer.CombatTimeElapsed, startTime);
                    break;
                case TimerKeyType.TargetChanged:
                    wasTriggered = TriggerDetection.CheckForTargetChange(log, SourceTimer.Source, SourceTimer.SourceIsLocal, SourceTimer.Target, SourceTimer.TargetIsLocal);
                    break;
            }

            if (wasTriggered == TriggerType.Refresh && ActiveTimerInstancesForTimer.Any(t => t.TargetId == targetId) && SourceTimer.CanBeRefreshed)
            {
                var timerToRestart = ActiveTimerInstancesForTimer.First(t => t.TargetId == targetId);
                timerToRestart.Reset(log.TimeStamp);
            }
            if (wasTriggered == TriggerType.Start && !ActiveTimerInstancesForTimer.Any(t => t.TargetId == targetId) )
            {
                if(SourceTimer.TriggerType != TimerKeyType.EntityHP)
                    CreateTimerInstance(log.TimeStamp, targetAdendum, targetId);
                else
                    CreateTimerInstance(currentHP,targetAdendum, targetId);
            }
            if(wasTriggered == TriggerType.End)
            {
                var hpTimer = ActiveTimerInstancesForTimer.FirstOrDefault(t => t.TargetId == targetId);
                if (hpTimer == null)
                    return;
                hpTimer.Complete();
            }



        }
        private void CreateTimerInstance(DateTime timeStamp, string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += Reset;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            ActiveTimerInstancesForTimer.Add(timerVM);
            timerVM.Trigger(timeStamp);
            NewTimerInstance(timerVM);
        }
        private void CreateTimerInstance(double currentHP,string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += Reset;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            ActiveTimerInstancesForTimer.Add(timerVM);
            timerVM.Trigger(currentHP);
            NewTimerInstance(timerVM);
        }
    }
}
