﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using Timer = SWTORCombatParser.DataStructures.Timer;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Timers
{
    public class TimerTargetInfo
    {
        public string Name;
        public long Id;
    }
    public class TimerInstance
    {
        private bool historicalParseEnded;
        private bool _singleUseTriggerUsed;
        private List<long> _alreadyDetectedEntities = new List<long>();
        private AbsorbShieldManager _absorbShieldManager;
        private TimerInstance expirationTimer;
        private TimerInstance cancelTimer;
        private IDisposable _expirationSub;
        public event Action<TimerInstanceViewModel> NewTimerInstance = delegate { };
        public event Action ReorderRequested = delegate {  };
        public event Action<TimerInstanceViewModel, bool> TimerOfTypeExpired = delegate { };
        public Timer SourceTimer;
        private readonly Dictionary<Guid, TimerInstanceViewModel> _activeTimerInstancesForTimer = new Dictionary<Guid, TimerInstanceViewModel>();
        private (string, string, string) _currentBossInfo;
        private EncounterInfo _currentEncounter;
        private TimerInstance parentTimer;

        public bool TrackOutsideOfCombat { get; set; }
        public bool IsEnabled { get; set; }
        public string ExperiationTimerId { get; set; }
        public TimerInstance ExpirationTimer
        {
            get => expirationTimer; set
            {
                expirationTimer = value;
                _expirationSub?.Dispose();
                _expirationSub = Observable
                    .FromEvent<Action<TimerInstanceViewModel, bool>, Tuple<TimerInstanceViewModel, bool>>(
                        onNextHandler => (p1, p2) => onNextHandler(Tuple.Create(p1, p2)),
                        manager => expirationTimer.TimerOfTypeExpired += manager,
                        manager => expirationTimer.TimerOfTypeExpired -= manager
                    ).Subscribe(args => ExpirationTimerEnded(args.Item1, args.Item2));
            }
        }

        private void ExpirationTimerEnded(TimerInstanceViewModel vm, bool endedNatrually)
        {
            if (SourceTimer.ShouldModifyVariable)
            {
                ModifyVariable(SourceTimer);
                if (!SourceTimer.UseVisualsAndModify)
                    return;
            }
            if (endedNatrually && CheckEncounterAndBoss(this,_currentEncounter))
            {
                CreateTimerNoTarget(DateTime.Now);
            }
        }
        public string CancellationTimerId { get; set; }
        public TimerInstance CancelTimer
        {
            get => cancelTimer; set
            {
                cancelTimer = value;
                cancelTimer.NewTimerInstance += _ =>
                {
                    Cancel();
                };
            }
        }
        public string ParentTimerId { get; set; }
        public TimerInstance ParentTimer
        {
            get => parentTimer; set
            {
                parentTimer = value;
                parentTimer.NewTimerInstance += _ =>
                {
                    Cancel();
                };
                parentTimer.ReorderRequested += Cancel;
            }
        }

        public TimerInstance(Timer sourceTimer)
        {
            SourceTimer = sourceTimer;
            ExperiationTimerId = sourceTimer.ExperiationTimerId;
            CancellationTimerId = sourceTimer.SelectedCancelTimerId;
            ParentTimerId = sourceTimer.ParentTimerId;
            IsEnabled = sourceTimer.IsEnabled;
            TrackOutsideOfCombat = sourceTimer.TrackOutsideOfCombat;
            CombatLogStreamer.CombatUpdated += UpdateCombatState;
            CombatLogStreamer.HistoricalLogsFinished += FinishedHistoricalLogs;
            CombatLogStreamer.HistoricalLogsStarted += StartedHistoricalLogs;
        }


        private void StartedHistoricalLogs()
        {
            historicalParseEnded = false;
        }

        private void FinishedHistoricalLogs(DateTime arg1, bool arg2)
        {
            historicalParseEnded = true;
        }

        public void Cancel()
        {
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
                _singleUseTriggerUsed = true;
            var currentActiveTimers = _activeTimerInstancesForTimer.Values.ToList();
            currentActiveTimers.ForEach(t => t.Complete(false));
        }

        public void UnCancel()
        {
            _singleUseTriggerUsed = false;
        }
        private void CompleteTimer(TimerInstanceViewModel timer, bool endedNatrually)
        {
            TimerOfTypeExpired(timer, endedNatrually);
            _activeTimerInstancesForTimer.Remove(timer.TimerId);
            timer.Dispose();
        }

        public void CheckForTrigger(ParsedLogEntry log, DateTime startTime, string currentDiscipline, List<TimerInstanceViewModel> activeTimers, EncounterInfo currentEncounter, (string,string,string) bossData)
        {
            _currentEncounter = currentEncounter;
            if(bossData.Item1 != "")
                UpdateBossInfo(bossData,log.TimeStamp);
            if (SourceTimer.Name.Contains("Other's") &&
                currentDiscipline is not ("Bodyguard" or "Combat Medic"))
                return;
            if ((SourceTimer.IsMechanic && !CheckEncounterAndBoss(this, _currentEncounter)) || SourceTimer.TriggerType == TimerKeyType.CombatStart)
                return;
            if (!IsEnabled || _singleUseTriggerUsed)
                return;

            if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield)
            {
                var damage = _absorbShieldManager?.CheckForDamage(log);
                if (damage.HasValue)
                {
                    if(_activeTimerInstancesForTimer.Any())
                        _activeTimerInstancesForTimer.FirstOrDefault().Value.DamageDoneToAbsorb += damage.Value;
                }
            }
                
            
            var wasTriggered = TriggerDetection.CheckForTrigger(log, SourceTimer, startTime, activeTimers,_alreadyDetectedEntities);

            if (wasTriggered == TriggerType.None && log.Effect.EffectType != EffectType.ModifyCharges)
                return;

            if (SourceTimer.ShouldModifyVariable && wasTriggered == TriggerType.Start)
            {
                ModifyVariable(SourceTimer);
                if(!SourceTimer.UseVisualsAndModify)
                    return;
            }

            var targetInfo = GetTargetInfo(log, SourceTimer, wasTriggered);
            
            if(wasTriggered == TriggerType.Start && SourceTimer.TriggerType == TimerKeyType.NewEntitySpawn)
                _alreadyDetectedEntities.Add(targetInfo.Id);
            
            if (wasTriggered == TriggerType.Refresh && SourceTimer.CanBeRefreshed)
            {
                var timerToRestart = _activeTimerInstancesForTimer
                    .FirstOrDefault(t => t.Value.TargetId == targetInfo.Id)
                    .Value;
                if (timerToRestart != null)
                {
                    if (log.TimeStamp - timerToRestart.StartTime < TimeSpan.FromSeconds(1))
                        return;
                    timerToRestart.Reset(log.TimeStamp);
                    ReorderRequested();
                }
            }

            if (SourceTimer.TriggerType == TimerKeyType.EntityHP && wasTriggered == TriggerType.Start)
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
                    CreateHPTimerInstance(currentHP, targetInfo.Name, targetInfo.Id);
                }
            }

            if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield && wasTriggered == TriggerType.Start)
            {
                _absorbShieldManager = new AbsorbShieldManager(log.Source);
                CreateAbsorbTimerInstance(SourceTimer.AbsorbValue, SourceTimer.Ability, log.Source.Id);
            }
            if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield && wasTriggered == TriggerType.End)
            {
                if (_activeTimerInstancesForTimer.Any(t => t.Value.TargetId == targetInfo.Id))
                {
                    var absorbTimer = _activeTimerInstancesForTimer.First(t => t.Value.TargetId == targetInfo.Id);
                    absorbTimer.Value.Complete(true);
                }
                _absorbShieldManager = null;
            }
            if (wasTriggered == TriggerType.Start &&
                _activeTimerInstancesForTimer.All(t => t.Value.TargetId != targetInfo.Id))
            {
                if (SourceTimer.TriggerType == TimerKeyType.FightDuration)
                {
                    _singleUseTriggerUsed = true;
                }

                if (SourceTimer.TriggerType != TimerKeyType.FightDuration)
                {
                    if (SourceTimer.TriggerType == TimerKeyType.And || SourceTimer.TriggerType == TimerKeyType.Or)
                    {
                        if(!_activeTimerInstancesForTimer.Any())
                            CreateTimerInstance(log, targetInfo.Name, targetInfo.Id);
                    }
                    else
                        CreateTimerInstance(log, targetInfo.Name, targetInfo.Id);
                }
            }

            if (wasTriggered == TriggerType.Start &&
                _activeTimerInstancesForTimer.Any(t => t.Value.TargetId == targetInfo.Id) &&
                (SourceTimer.TriggerType == TimerKeyType.AbilityUsed || SourceTimer.TriggerType == TimerKeyType.And || SourceTimer.TriggerType == TimerKeyType.Or))
            {
                var timerToRefresh = _activeTimerInstancesForTimer.First(t => t.Value.TargetId == targetInfo.Id).Value;
                timerToRefresh.Reset(log.TimeStamp);
                ReorderRequested();
            }

            if (wasTriggered == TriggerType.End)
            {
                var endedTimer = _activeTimerInstancesForTimer.FirstOrDefault(t => t.Value.TargetId == targetInfo.Id).Value;

                if (endedTimer == null)
                {
                    return;
                }

                if (log.TimeStamp - endedTimer.StartTime < TimeSpan.FromSeconds(1))
                {
                    return;
                }
                endedTimer.Complete(true);
            }

            if (log.Effect.EffectType == EffectType.ModifyCharges || (log.Effect.EffectType == EffectType.Apply &&
                log.Effect.EffectId != _7_0LogParsing._damageEffectId && log.Effect.EffectId != _7_0LogParsing._healEffectId && (int)log.Value.DblValue != 0))
            {
                UpdateCharges(log, targetInfo);
            }
            if(wasTriggered == TriggerType.Start)
                Task.Run(() =>
                {
                    Thread.Sleep(250);
                    ReorderRequested();
                });
        }

        private void ModifyVariable(Timer sourceTimer)
        {
            switch (sourceTimer.ModifyVariableAction)
            {
                case VariableModifications.Add:
                    VariableManager.AddToVariable(sourceTimer.ModifyVariableName, sourceTimer.VariableModificationValue);
                    break;
                case VariableModifications.Subtract:
                    VariableManager.AddToVariable(sourceTimer.ModifyVariableName, sourceTimer.VariableModificationValue > 0 ? sourceTimer.VariableModificationValue * -1 : sourceTimer.VariableModificationValue);
                    break;
                case VariableModifications.Set:
                    VariableManager.SetVariable(sourceTimer.ModifyVariableName, sourceTimer.VariableModificationValue);
                    break;
            }
        }

        private void UpdateCharges(ParsedLogEntry log, TimerTargetInfo targetInfo)
        {
            var timerToUpdate = _activeTimerInstancesForTimer.FirstOrDefault(t =>
                t.Value.TargetId == targetInfo.Id && (t.Value.SourceTimer.Effect == log.Effect.EffectName || t.Value.SourceTimer.Effect == log.Effect.EffectId)).Value;
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
            Entity playerTarget =
                CombatLogStateBuilder.CurrentState.GetLocalPlayerTargetAtTime(log.TimeStamp).Entity;
            if (sourceTimer.TriggerType == TimerKeyType.AbsorbShield)
            {
                return new TimerTargetInfo()
                {
                    Name = log.Source.Name,
                    Id = log.Source.Id
                };
            }

            if (sourceTimer.TriggerType == TimerKeyType.NewEntitySpawn && triggerType == TriggerType.Start)
            {
                var newEntityInfo = TriggerDetection.GetTargetId(log, SourceTimer.Source,playerTarget);
                return new TimerTargetInfo()
                {
                    Name = newEntityInfo.Name,
                    Id = newEntityInfo.Id
                };
            }
            if (sourceTimer.TriggerType == TimerKeyType.EntityHP && triggerType != TriggerType.None)
            {
                var hpTargetInfo = TriggerDetection.GetTargetId(log, SourceTimer.Target,
                    playerTarget);
                return new TimerTargetInfo()
                {
                    Name = hpTargetInfo.Name,
                    Id = hpTargetInfo.Id
                };
            }
            if ((triggerType == TriggerType.Refresh && SourceTimer.CanBeRefreshed) || (triggerType == TriggerType.Start && (SourceTimer.TriggerType == TimerKeyType.And || SourceTimer.TriggerType == TimerKeyType.Or)))
            {
                var currentTarget =
                    CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp).Entity;
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
        private void UpdateBossInfo((string, string, string) bossInfoParts, DateTime combatStart)
        {
            if (_currentBossInfo != bossInfoParts && SourceTimer.TriggerType == TimerKeyType.CombatStart)
            {
                _currentBossInfo = bossInfoParts;
                var currentEnecounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(combatStart);
                if (CheckEncounterAndBoss(this, currentEnecounter) && !_activeTimerInstancesForTimer.Any())
                {
                    CreateTimerNoTarget(combatStart);
                }
            }
            _currentBossInfo = bossInfoParts;
        }
        private void UpdateCombatState(CombatStatusUpdate obj)
        {
            if (obj.Type == UpdateType.Stop && historicalParseEnded)
            { 
                _currentBossInfo = ("", "", "");
                _alreadyDetectedEntities.Clear();
            }
        }
        private bool CheckEncounterAndBoss(TimerInstance t, EncounterInfo encounter)
        {
            var timerEncounter = t.SourceTimer.SpecificEncounter;
            var supportedDifficulties = new List<string>();
            if (t.SourceTimer.ActiveForStory)
                supportedDifficulties.Add("Story");
            if (t.SourceTimer.ActiveForVeteran)
                supportedDifficulties.Add("Veteran");
            if (t.SourceTimer.ActiveForMaster)
                supportedDifficulties.Add("Master");
            var timerBoss = t.SourceTimer.SpecificBoss;
            if (timerEncounter == "All")
                return true;

            if (encounter.Name == timerEncounter && (supportedDifficulties.Contains(encounter.Difficutly)) && _currentBossInfo.Item1 == timerBoss)
                return true;
            return false;
        }

        public void CreateTimerNoTarget(DateTime startTime)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = startTime;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerTimeTimer(startTime);
            NewTimerInstance(timerVM);
        }
        private void CreateTimerInstance(ParsedLogEntry log, string targetAdendum, long targetId)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = log.TimeStamp;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerTimeTimer(log.TimeStamp);
            NewTimerInstance(timerVM);
        }
        private void CreateHPTimerInstance(double currentHP, string targetAdendum, long targetId)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerHPTimer(currentHP);
            NewTimerInstance(timerVM);
        }

        private void CreateAbsorbTimerInstance(double maxAbsorb, string abilityName, long targetId)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TargetAddendem = abilityName;
            timerVM.TargetId = targetId;
            _activeTimerInstancesForTimer[timerVM.TimerId] = timerVM;
            timerVM.TriggerAbsorbTimer(maxAbsorb);
            NewTimerInstance(timerVM);
        }
    }
}
