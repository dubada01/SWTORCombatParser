using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers
{
    public class TimerInstance
    {
        private bool _isCancelled;
        private TimerInstance expirationTimer;
        public event Action<TimerInstanceViewModel> NewTimerInstance = delegate { };
        public event Action<TimerInstanceViewModel> TimerOfTypeExpired = delegate { };
        public Timer SourceTimer;
        private readonly Dictionary<Guid,TimerInstanceViewModel> _activeTimerInstancesForTimer = new Dictionary<Guid,TimerInstanceViewModel>();
        private (string, string, string) _currentBossInfo;
        public bool TrackOutsideOfCombat { get; set; }
        public bool IsEnabled { get; set; }
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
            CombatIdentifier.NewCombatAvailable += UpdateBossInfo;
        }

        public void Cancel()
        {
            _isCancelled = true;
            var currentActiveTimers = _activeTimerInstancesForTimer.Values.ToList();
            currentActiveTimers.ForEach(t=>t.Complete());
        }

        public void UnCancel()
        {
            _isCancelled = false;
        }

        private void CompleteTimer(TimerInstanceViewModel timer)
        {
            if (timer.SourceTimer.TriggerType == TimerKeyType.FightDuration)
                _isCancelled = true;
            TimerOfTypeExpired(timer);
            _activeTimerInstancesForTimer.Remove(timer.TimerId);
            timer.Dispose();
        }

        public void CheckForTrigger(ParsedLogEntry log, DateTime startTime)
        {            
            var currentDiscipline =
                CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(log.TimeStamp).Discipline;
            if (SourceTimer.Name.Contains("Other's") &&
                currentDiscipline is not ("Bodyguard" or "Combat Medic"))
                return;
            if (SourceTimer.IsMechanic && !CheckEncounterAndBoss(this,
                    CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp)))
                return;
            TriggerType wasTriggered = TriggerType.None;
            if (!IsEnabled || _isCancelled)
                return;
            var targetAdendum = "";
            long targetId = 0;
            double currentHP = 100;
            
            wasTriggered = WasTriggered(log, startTime, ref targetAdendum, ref targetId);

            if (wasTriggered == TriggerType.Refresh && SourceTimer.CanBeRefreshed)
            {
                var timerToRestart = _activeTimerInstancesForTimer.FirstOrDefault(t => t.Value.TargetId == targetId)
                    .Value;

                if (SourceTimer.IsHot && !CombatLogStateBuilder.CurrentState
                        .GetPlayerTargetAtTime(log.Source, log.TimeStamp).IsCharacter)
                {
                    var localPlayerid = CombatLogStateBuilder.CurrentState.LocalPlayer.Id;
                    timerToRestart = _activeTimerInstancesForTimer
                        .FirstOrDefault(t => t.Value.TargetId == localPlayerid).Value;
                }

                if (timerToRestart != null)
                {                
                    if (log.TimeStamp - timerToRestart.StartTime < TimeSpan.FromSeconds(1))
                        return;
                    timerToRestart.Reset(log.TimeStamp);
                }
                else
                {
                    if (SourceTimer.IsHot && log.Target.IsCharacter)
                    {
                        var timerVm = CreateTimerInstance(log.TimeStamp, targetAdendum, targetId);
                        TimerNotifier.FireTimerTriggered(timerVm);
                    }
                }
            }

            
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
            {
                if (wasTriggered != TriggerType.None)
                {
                    var entity = TriggerDetection.GetTargetId(log, SourceTimer.Target,
                        SourceTimer.TargetIsLocal, SourceTimer.TargetIsAnyButLocal);
                    targetAdendum = entity.Name;
                    targetId = entity.Id;
                }

                if (wasTriggered == TriggerType.Start)
                {
                    currentHP = TriggerDetection.GetCurrentTargetHPPercent(log, targetId);
                    var hpTimer = _activeTimerInstancesForTimer.FirstOrDefault(t =>
                            t.Value.SourceTimer.TriggerType == TimerKeyType.EntityHP &&
                            t.Value.TargetId == targetId)
                        .Value;
                    if (hpTimer != null)
                    {
                        hpTimer.CurrentMonitoredHP = currentHP;
                    }
                }
            }
            if (wasTriggered == TriggerType.Start &&
                _activeTimerInstancesForTimer.All(t => t.Value.TargetId != targetId))
            {
                var timerVm = SourceTimer.TriggerType != TimerKeyType.EntityHP
                    ? CreateTimerInstance(log.TimeStamp, targetAdendum, targetId)
                    : CreateHPTimerInstance(log.TimeStamp, currentHP, targetAdendum, targetId);
                TimerNotifier.FireTimerTriggered(timerVm);
            }

            if (wasTriggered == TriggerType.Start &&
                _activeTimerInstancesForTimer.Any(t => t.Value.TargetId == targetId) &&
                SourceTimer.TriggerType == TimerKeyType.AbilityUsed)
            {
                var timerToRefresh = _activeTimerInstancesForTimer.First(t => t.Value.TargetId == targetId).Value;
                timerToRefresh.Reset(log.TimeStamp);
                //TimerNotifier.FireTimerTriggered(timerVm);
            }

            if (wasTriggered == TriggerType.End)
            {
                var endedTimer = _activeTimerInstancesForTimer.FirstOrDefault(t => t.Value.TargetId == targetId).Value;

                if (endedTimer == null)
                    return;
                if (log.TimeStamp - endedTimer.StartTime < TimeSpan.FromSeconds(1))
                    return;
                endedTimer.Complete();
            }

            if (log.Effect.EffectType == EffectType.ModifyCharges || log.Effect.EffectType == EffectType.Apply &&
                log.Effect.EffectId != _7_0LogParsing._damageEffectId && log.Effect.EffectId != _7_0LogParsing._healEffectId)
            {
                var timerToUpdate = _activeTimerInstancesForTimer.FirstOrDefault(t =>
                    t.Value.TargetId == targetId && t.Value.SourceTimer.Effect == log.Effect.EffectName).Value;
                if (timerToUpdate == null)
                    return;
                timerToUpdate.Charges = (int)log.Value.DblValue;
            }

        }

        private TriggerType WasTriggered(ParsedLogEntry log, DateTime startTime, ref string targetAdendum, ref long targetId)
        {
            TriggerType wasTriggered = TriggerType.None;
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.CombatStart:
                    wasTriggered = TriggerDetection.CheckForComabatStart(log);
                    break;
                case TimerKeyType.AbilityUsed:
                    wasTriggered = TriggerDetection.CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source,
                        SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.EffectGained:
                    wasTriggered = TriggerDetection.CheckForEffectGain(log, SourceTimer.Effect,
                        SourceTimer.AbilitiesThatRefresh, SourceTimer.Source, SourceTimer.Target,
                        SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal, SourceTimer.SourceIsAnyButLocal,
                        SourceTimer.TargetIsAnyButLocal);

                    if (wasTriggered == TriggerType.Refresh)
                    {
                        var currentTarget =
                            CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp);
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
                    wasTriggered = TriggerDetection.CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Target,
                        SourceTimer.TargetIsLocal, SourceTimer.SourceIsAnyButLocal,
                        SourceTimer.TargetIsAnyButLocal);
                    targetAdendum = log.Target.Name;
                    targetId = log.Target.Id;
                    break;
                case TimerKeyType.EntityHP:
                    wasTriggered = TriggerDetection.CheckForHP(log, SourceTimer.HPPercentage,
                        SourceTimer.HPPercentageDisplayBuffer, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.TargetIsAnyButLocal);


                    break;
                case TimerKeyType.FightDuration:
                    wasTriggered =
                        TriggerDetection.CheckForFightDuration(log, SourceTimer.CombatTimeElapsed, startTime);
                    break;
                case TimerKeyType.TargetChanged:
                    wasTriggered = TriggerDetection.CheckForTargetChange(log, SourceTimer.Source,
                        SourceTimer.SourceIsLocal, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal);
                    break;
                case TimerKeyType.DamageTaken:
                    wasTriggered = TriggerDetection.CheckForDamageTaken(log, SourceTimer.Source,
                        SourceTimer.SourceIsLocal, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal, SourceTimer.Ability);
                    break;
                case TimerKeyType.HasEffect:
                    wasTriggered = TriggerDetection.CheckForHasEffect(log, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.TargetIsAnyButLocal, SourceTimer.Effect);
                    break;
                case TimerKeyType.And:
                case TimerKeyType.Or:
                    wasTriggered = TriggerDetection.CheckForDualEffect(SourceTimer,log, SourceTimer.TriggerType, startTime);
                    break;
            }

            return wasTriggered;
        }

        private void UpdateBossInfo(Combat obj)
        {
            if (obj != null && obj.IsCombatWithBoss)
                _currentBossInfo = CombatIdentifier.CurrentCombat.EncounterBossDifficultyParts;
            else
                _currentBossInfo = ("", "", "");
        }        
        private bool CheckEncounterAndBoss(TimerInstance t, EncounterInfo encounter)
        {
            var timerEncounter = t.SourceTimer.SpecificEncounter;
            var timerDifficulty = t.SourceTimer.SpecificDifficulty;
            var timerBoss = t.SourceTimer.SpecificBoss;
            if (timerEncounter == "All")
                return true;

            if (encounter.Name == timerEncounter && (encounter.Difficutly == timerDifficulty || timerDifficulty == "All") && _currentBossInfo.Item1 == timerBoss)
                return true;
            return false;
        }
        private TimerInstanceViewModel CreateTimerInstance(DateTime timeStamp, string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = timeStamp;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerTimeTimer(timeStamp);
            NewTimerInstance(timerVM);
            return timerVM;
        }
        private TimerInstanceViewModel CreateHPTimerInstance(DateTime timeStamp, double currentHP,string targetAdendum = "", long targetId = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerHPTimer(currentHP);
            NewTimerInstance(timerVM);
            return timerVM;
        }
    }
}
