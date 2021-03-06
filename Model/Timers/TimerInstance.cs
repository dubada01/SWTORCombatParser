using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
                for (var i=0; i < ActiveTimerInstancesForTimer.Count; i++)
                    {
                    ActiveTimerInstancesForTimer[i].Complete();
                }
                //ActiveTimerInstancesForTimer.ForEach(t => t.Complete());
                //ActiveTimerInstancesForTimer.ForEach(t => TimerOfTypeExpired(t));
                //ActiveTimerInstancesForTimer.Clear();
            }
            else
            {
                //ActiveTimerInstancesForTimer.Remove(timer);
                //TimerOfTypeExpired(timer);
                //timer.Dispose();
                timer.Complete();
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
                timer.TriggerTimeTimer(DateTime.Now);
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
            if (wasTriggered == TriggerType.Refresh && SourceTimer.CanBeRefreshed)
            {

                var timerToRestart = ActiveTimerInstancesForTimer.FirstOrDefault(t => t.TargetId == targetId);
                if (SourceTimer.IsHot && !CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp).IsCharacter)
                {
                    var localPlayerid = CombatLogStateBuilder.CurrentState.LocalPlayer.Id;
                    timerToRestart = ActiveTimerInstancesForTimer.FirstOrDefault(t => t.TargetId == localPlayerid);
                }
                if (timerToRestart != null)
                {
                    timerToRestart.Reset(log.TimeStamp);
                }
                else
                {
                    var currentTarget = CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp);
                    if (SourceTimer.IsHot && log.Target.IsCharacter)
                    {
                        var timerVm = CreateTimerInstance(log.TimeStamp, targetAdendum, targetId);
                        TimerNotifier.FireTimerTriggered(timerVm);
                    }
                }
            }
            if (wasTriggered == TriggerType.Start && !ActiveTimerInstancesForTimer.Any(t => t.TargetId == targetId) )
            {
                TimerInstanceViewModel timerVm;
                if(SourceTimer.TriggerType != TimerKeyType.EntityHP)
                    timerVm=CreateTimerInstance(log.TimeStamp, targetAdendum, targetId);
                else
                    timerVm=CreateHPTimerInstance(currentHP,targetAdendum, targetId);
                TimerNotifier.FireTimerTriggered(timerVm);
            }
            if(wasTriggered == TriggerType.End)
            {
                var endedTimer = ActiveTimerInstancesForTimer.FirstOrDefault(t => t.TargetId == targetId);

                if (endedTimer == null)
                    return;
                if (log.TimeStamp - endedTimer.StartTime < TimeSpan.FromSeconds(1))
                    return;
                endedTimer.Complete();
            }
            if (log.Effect.EffectType == EffectType.ModifyCharges || log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName != "Damage" && log.Effect.EffectName != "Heal")
            {
                var timerToUpdate = ActiveTimerInstancesForTimer.FirstOrDefault(t => t.TargetId == targetId && t.SourceTimer.Effect == log.Effect.EffectName);
                if (timerToUpdate == null)
                    return;
                timerToUpdate.Charges = (int)log.Value.DblValue;
            }


        }
        private TimerInstanceViewModel CreateTimerInstance(DateTime timeStamp, string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = timeStamp;
            timerVM.TimerExpired += Reset;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            ActiveTimerInstancesForTimer.Add(timerVM);
            timerVM.TriggerTimeTimer(timeStamp);
            NewTimerInstance(timerVM);
            return timerVM;
        }
        private TimerInstanceViewModel CreateHPTimerInstance(double currentHP,string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += Reset;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            ActiveTimerInstancesForTimer.Add(timerVM);
            timerVM.TriggerHPTimer(currentHP);
            NewTimerInstance(timerVM);
            return timerVM;
        }
    }
}
