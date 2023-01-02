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
    public class TimerTargetInfo
    {
        public string Name;
        public long Id;
    }
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
                expirationTimer.TimerOfTypeExpired += _ => CreateBlankTimer(DateTime.Now);
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

        public void CheckForTrigger(ParsedLogEntry log, DateTime startTime,string currentDiscipline)
        {
            if (SourceTimer.Name.Contains("Other's"))
                Console.WriteLine("test");
            if (SourceTimer.Name.Contains("Other's") &&
                currentDiscipline is not ("Bodyguard" or "Combat Medic"))
                return;
            if (SourceTimer.IsMechanic && !CheckEncounterAndBoss(this,
                    CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp)))
                return;
            if (!IsEnabled || _isCancelled)
                return;
            
            var wasTriggered = TriggerDetection.CheckForTrigger(log, SourceTimer, startTime);
            if (wasTriggered == TriggerType.None && log.Effect.EffectType != EffectType.ModifyCharges)
                return;
            var targetInfo = GetTargetInfo(log, SourceTimer, wasTriggered);
            
            if (wasTriggered == TriggerType.Refresh && SourceTimer.CanBeRefreshed)
            {
                var timerToRestart = _activeTimerInstancesForTimer.FirstOrDefault(t => t.Value.TargetId == targetInfo.Id)
                    .Value;

                /*if (SourceTimer.IsHot && !CombatLogStateBuilder.CurrentState
                        .GetPlayerTargetAtTime(log.Source, log.TimeStamp).IsCharacter)
                {
                    var localPlayerid = CombatLogStateBuilder.CurrentState.LocalPlayer.Id;
                    timerToRestart = _activeTimerInstancesForTimer
                        .FirstOrDefault(t => t.Value.TargetId == localPlayerid).Value;
                }*/

                if (timerToRestart != null)
                {                
                    if (log.TimeStamp - timerToRestart.StartTime < TimeSpan.FromSeconds(1))
                        return;
                    timerToRestart.Reset(log.TimeStamp);
                }
                else
                {
                    /*if (SourceTimer.IsHot && log.Target.IsCharacter)
                    {
                        var timerVm = CreateTimerInstance(log);
                        TimerNotifier.FireTimerTriggered(timerVm);
                    }*/
                }
            }

            
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
            {
                if (wasTriggered == TriggerType.Start)
                {                    
                    var currentHP = TriggerDetection.GetCurrentTargetHPPercent(log, targetInfo.Id);
                    var hpTimer = _activeTimerInstancesForTimer.FirstOrDefault(t =>
                            t.Value.SourceTimer.TriggerType == TimerKeyType.EntityHP &&
                            t.Value.TargetId == targetInfo.Id)
                        .Value;
                    if (hpTimer != null)
                    {
                        hpTimer.CurrentMonitoredHP = currentHP;
                    }

                    if (_activeTimerInstancesForTimer.All(t => t.Value.TargetId != targetInfo.Id))
                    {
                        CreateHPTimerInstance(log.TimeStamp, currentHP, targetInfo.Name, targetInfo.Id);
                    }
                }
            }
            if (wasTriggered == TriggerType.Start &&
                _activeTimerInstancesForTimer.All(t => t.Value.TargetId != log.Target.Id))
            {
                var vm = CreateTimerInstance(log);
                TimerNotifier.FireTimerTriggered(vm);
            }

            if (wasTriggered == TriggerType.Start &&
                _activeTimerInstancesForTimer.Any(t => t.Value.TargetId == targetInfo.Id) &&
                SourceTimer.TriggerType == TimerKeyType.AbilityUsed)
            {
                var timerToRefresh = _activeTimerInstancesForTimer.First(t => t.Value.TargetId == targetInfo.Id).Value;
                timerToRefresh.Reset(log.TimeStamp);
            }

            if (wasTriggered == TriggerType.End)
            {
                var endedTimer = _activeTimerInstancesForTimer.FirstOrDefault(t => t.Value.TargetId == targetInfo.Id).Value;

                if (endedTimer == null)
                    return;
                if (log.TimeStamp - endedTimer.StartTime < TimeSpan.FromSeconds(1))
                    return;
                endedTimer.Complete();
            }

            if (log.Effect.EffectType == EffectType.ModifyCharges || (log.Effect.EffectType == EffectType.Apply &&
                log.Effect.EffectId != _7_0LogParsing._damageEffectId && log.Effect.EffectId != _7_0LogParsing._healEffectId && (int)log.Value.DblValue!=0))
            {
                UpdateCharges(log, targetInfo);
            }

        }

        private void UpdateCharges(ParsedLogEntry log, TimerTargetInfo targetInfo)
        {
            var timerToUpdate = _activeTimerInstancesForTimer.FirstOrDefault(t =>
                t.Value.TargetId == targetInfo.Id && (t.Value.SourceTimer.Effect == log.Effect.EffectName||t.Value.SourceTimer.Effect == log.Effect.EffectId)).Value;
            if (timerToUpdate == null)
                return;
            if (log.Effect.EffectId == "985226842996736" && log.Effect.EffectType == EffectType.Apply)
                timerToUpdate.Charges = 7;
            else
                timerToUpdate.Charges = (int)log.Value.DblValue;
        }
        private TimerTargetInfo GetTargetInfo(ParsedLogEntry log, Timer sourceTimer, TriggerType triggerType)
        {
            if (triggerType == TriggerType.None && log.Effect.EffectType != EffectType.ModifyCharges)
                return new TimerTargetInfo();
            if(sourceTimer.TriggerType == TimerKeyType.EntityHP)
            {
                var hpTargetInfo = TriggerDetection.GetTargetId(log, SourceTimer.Target,
                    SourceTimer.TargetIsLocal, SourceTimer.TargetIsAnyButLocal);
                return new TimerTargetInfo()
                {
                    Name = hpTargetInfo.Name,
                    Id = hpTargetInfo.Id
                };
            }
            if(triggerType == TriggerType.Refresh && SourceTimer.CanBeRefreshed)
            {
                var currentTarget =
                    CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp);
                return new TimerTargetInfo()
                {
                    Name = currentTarget.Name,
                    Id = currentTarget.Id
                };
            }

            return new TimerTargetInfo()
            {
                Name = log.Target.Name,
                Id = log.Target.Id
            };
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

        private TimerInstanceViewModel CreateBlankTimer(DateTime startTime)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = startTime;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerTimeTimer(startTime);
            NewTimerInstance(timerVM);
            return timerVM;
        }
        private TimerInstanceViewModel CreateTimerInstance(ParsedLogEntry log)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = log.TimeStamp;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TargetAddendem = log.Target.Name;
            timerVM.TargetId = log.Target.Id;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerTimeTimer(log.TimeStamp);
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
