using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        // Fields
        private bool _hasHistoricalParseEnded;
        private bool _isSingleUseTriggerUsed;
        private readonly List<long> _alreadyDetectedEntities = new();
        private AbsorbShieldManager? _absorbShieldManager;
        private TimerInstance _expirationTimer;
        private TimerInstance _cancelTimer;
        private IDisposable _expirationSubscription;
        private readonly Dictionary<Guid, TimerInstanceViewModel> _activeTimerInstances = new();
        private (string, string, string) _currentBossInfo;
        private Entity _currentTarget;
        private ConcurrentDictionary<string, TimerInstanceViewModel> _activeTimers;
        private DateTime _startTime;
        private EncounterInfo _currentEncounter;
        private TimerInstance _parentTimer;
        private readonly object _timerChangeLock = new();
        private bool _isCombatStarted;

        // Properties
        public bool TrackOutsideOfCombat { get; set; }
        public bool IsEnabled { get; set; }
        public string ExpirationTimerId { get; set; }
        public string CancellationTimerId { get; set; }
        public string ParentTimerId { get; set; }
        public Timer SourceTimer { get; }

        public TimerInstance CancelTimer
        {
            get => _cancelTimer;
            set
            {
                _cancelTimer = value;
                _cancelTimer.Triggered += () => Cancel();
            }
        }

        public TimerInstance ParentTimer
        {
            get => _parentTimer;
            set
            {
                _parentTimer = value;
                _parentTimer.Triggered += () => Cancel();
                _parentTimer.ReorderRequested += Cancel;
            }
        }

        // Events
        public event Action<TimerInstanceViewModel> NewTimerInstance = delegate { };
        public event Action Triggered = delegate { };
        public event Action<string> ReorderRequested = delegate { };
        public event Action<TimerInstanceViewModel, bool> TimerOfTypeExpired = delegate { };

        // Constructor
        public TimerInstance(Timer sourceTimer)
        {
            SourceTimer = sourceTimer;
            ExpirationTimerId = sourceTimer.ExpirationTimerId;
            CancellationTimerId = sourceTimer.SelectedCancelTimerId;
            ParentTimerId = sourceTimer.ParentTimerId;
            IsEnabled = sourceTimer.IsEnabled;
            TrackOutsideOfCombat = sourceTimer.TrackOutsideOfCombat;
            CombatLogStreamer.CombatUpdated += UpdateCombatState;
            CombatLogStreamer.HistoricalLogsFinished += FinishedHistoricalLogs;
            CombatLogStreamer.HistoricalLogsStarted += StartedHistoricalLogs;
        }

        // Methods
        public void TryTriggerParent()
        {
            if (!SourceTimer.IsSubTimer)
            {
                if (SourceTimer.ShouldModifyVariable)
                {
                    ModifyVariable(SourceTimer);
                    if (!SourceTimer.UseVisualsAndModify)
                        return;
                }

                CreateTimerNoTarget(TimeUtility.CorrectedTime);
            }
            else
            {
                bool fromClause1 = ParentTimer.SourceTimer.Clause1.Id == SourceTimer.Id;
                bool fromClause2 = ParentTimer.SourceTimer.Clause2.Id == SourceTimer.Id;
                var triggerState = TriggerDetection.CheckForTriggerNoLog(ParentTimer.SourceTimer, _startTime, _activeTimers, _alreadyDetectedEntities, _currentTarget, fromClause1, fromClause2);
                if (triggerState == TriggerType.Start)
                {
                    ParentTimer.TryTriggerParent();
                }
            }
        }

        public void ExpirationTimerEnded(TimerInstanceViewModel vm, bool endedNaturally)
        {
            if (SourceTimer.ShouldModifyVariable)
            {
                ModifyVariable(SourceTimer);
                if (!SourceTimer.UseVisualsAndModify)
                    return;
            }
            if (endedNaturally && CheckEncounterAndBoss(this, _currentEncounter))
            {
                TryTriggerParent();
            }
        }

        public void CombatEnd()
        {
            _currentBossInfo = ("", "", "");
            _isCombatStarted = false;
        }

        public void Cancel(string id = "")
        {
            if (SourceTimer.TriggerType == TimerKeyType.EntityHP)
                _isSingleUseTriggerUsed = true;
            lock (_timerChangeLock)
            {
                var currentActiveTimers = _activeTimerInstances.Values.ToList();
                currentActiveTimers.ForEach(t => t.Complete(false));
            }
        }

        public void UnCancel()
        {
            _isSingleUseTriggerUsed = false;
        }

        private void StartedHistoricalLogs()
        {
            _hasHistoricalParseEnded = false;
        }

        private void FinishedHistoricalLogs(DateTime arg1, bool arg2)
        {
            _hasHistoricalParseEnded = true;
        }

        private void UpdateCombatState(CombatStatusUpdate obj)
        {
            if (obj.Type == UpdateType.Stop && _hasHistoricalParseEnded)
            {
                _currentBossInfo = ("", "", "");
                _alreadyDetectedEntities.Clear();
            }
        }

        private void ModifyVariable(Timer sourceTimer)
        {
            switch (sourceTimer.ModifyVariableAction)
            {
                case VariableModifications.Add:
                    OrbsVariableManager.AddToVariable(sourceTimer.ModifyVariableName, sourceTimer.VariableModificationValue);
                    break;
                case VariableModifications.Subtract:
                    OrbsVariableManager.AddToVariable(sourceTimer.ModifyVariableName, sourceTimer.VariableModificationValue > 0 ? sourceTimer.VariableModificationValue * -1 : sourceTimer.VariableModificationValue);
                    break;
                case VariableModifications.Set:
                    OrbsVariableManager.SetVariable(sourceTimer.ModifyVariableName, sourceTimer.VariableModificationValue);
                    break;
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
            if (string.IsNullOrEmpty(_currentBossInfo.Item1))
            {
                return false;
            }
            if (encounter.Name == timerEncounter && (supportedDifficulties.Contains(encounter.Difficutly)) && _currentBossInfo.Item1.ToLower() == timerBoss.ToLower())
                return true;
            return false;
        }

        public void CancelTimerEnded(TimerInstanceViewModel vm)
        {
            Cancel();
        }

        private void CompleteTimer(TimerInstanceViewModel timer, bool endedNaturally)
        {
            TimerOfTypeExpired.InvokeSafely(timer, endedNaturally);
            lock (_timerChangeLock)
            {
                _activeTimerInstances.Remove(timer.TimerId);
            }
            timer.Dispose();
        }

        public void CheckForTrigger(ParsedLogEntry log, DateTime startTime, string currentDiscipline, ConcurrentDictionary<string, TimerInstanceViewModel> activeTimers, EncounterInfo currentEncounter, (string, string, string) bossData, Entity currentTarget)
        {
            lock (_timerChangeLock)
            {
                _currentTarget = currentTarget;
                _activeTimers = activeTimers;
                _startTime = startTime;
                _currentEncounter = currentEncounter;
                if (bossData.Item1 != "")
                    UpdateBossInfo(bossData, log.TimeStamp);
                if (string.IsNullOrEmpty(SourceTimer.Name))
                    SourceTimer.Name = "Unknown Timer";
                if (SourceTimer.Name.Contains("Other's") &&
                    currentDiscipline is not ("Bodyguard" or "Combat Medic"))
                    return;
                if (!IsEnabled || _isSingleUseTriggerUsed || SourceTimer.TriggerType == TimerKeyType.CombatStart)
                    return;

                if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield)
                {
                    var damage = _absorbShieldManager?.CheckForDamage(log);
                    if (damage.HasValue)
                    {
                        if (_activeTimerInstances.Count > 0)
                            _activeTimerInstances.FirstOrDefault().Value.DamageDoneToAbsorb += damage.Value;
                    }
                }


                var wasTriggered = TriggerDetection.CheckForTrigger(log, SourceTimer, startTime, activeTimers, _currentTarget, _alreadyDetectedEntities);

                if (wasTriggered == TriggerType.None && log.Effect.EffectType != EffectType.ModifyCharges)
                    return;



                var targetInfo = GetTargetInfo(log, SourceTimer, wasTriggered, currentTarget);

                if (_activeTimerInstances.Any(t => t.Value.TargetId == targetInfo.Id) && SourceTimer.DontRefresh)
                    return;

                if (SourceTimer.ShouldModifyVariable && (wasTriggered == TriggerType.Start || (wasTriggered == TriggerType.Refresh && _activeTimerInstances.Any(t => t.Value.TargetId == targetInfo.Id))))
                {
                    ModifyVariable(SourceTimer);
                    if (!SourceTimer.UseVisualsAndModify)
                    {
                        Triggered.InvokeSafely();
                        return;
                    }
                }

                if (wasTriggered == TriggerType.Start && SourceTimer.TriggerType == TimerKeyType.NewEntitySpawn)
                    _alreadyDetectedEntities.Add(targetInfo.Id);


                if (wasTriggered == TriggerType.Start &&
    _activeTimerInstances.Any(t => t.Value.TargetId == targetInfo.Id) &&
    (SourceTimer.TriggerType == TimerKeyType.AbilityUsed || SourceTimer.TriggerType == TimerKeyType.And || SourceTimer.TriggerType == TimerKeyType.Or || SourceTimer.TriggerType == TimerKeyType.EffectGained))
                {
                    var timerToRefresh = _activeTimerInstances.First(t => t.Value.TargetId == targetInfo.Id).Value;
                    timerToRefresh.Reset(log.TimeStamp);
                    ReorderRequested.InvokeSafely(SourceTimer.Id);
                }

                if (wasTriggered == TriggerType.Refresh && SourceTimer.CanBeRefreshed)
                {
                    var timerToRestart = _activeTimerInstances
                        .FirstOrDefault(t => t.Value.TargetId == targetInfo.Id)
                        .Value;
                    if (timerToRestart != null)
                    {
                        if (log.TimeStamp - timerToRestart.StartTime < TimeSpan.FromSeconds(1))
                            return;
                        timerToRestart.Reset(log.TimeStamp);
                        ReorderRequested.InvokeSafely(SourceTimer.Id);
                    }
                    if (!_activeTimerInstances.Any())
                    {
                        CreateTimerInstance(log.TimeStamp, targetInfo.Name, targetInfo.Id);
                    }
                }

                if (SourceTimer.TriggerType == TimerKeyType.EntityHP && wasTriggered == TriggerType.Start)
                {
                    var currentHP = TriggerDetection.GetCurrentTargetHPPercent(log, targetInfo.Id);
                    var hpTimer = _activeTimerInstances.FirstOrDefault(t =>
                            t.Value.SourceTimer.TriggerType == TimerKeyType.EntityHP &&
                            t.Value.TargetId == targetInfo.Id)
                        .Value;
                    if (hpTimer != null)
                    {
                        hpTimer.CurrentMonitoredHP = currentHP;
                    }

                    if (_activeTimerInstances.All(t => t.Value.TargetId != targetInfo.Id))
                    {
                        CreateHPTimerInstance(currentHP, targetInfo.Name, targetInfo.Id);
                    }
                }

                if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield && wasTriggered == TriggerType.Start)
                {
                    if (_activeTimerInstances.All(t => t.Value.TargetId != targetInfo.Id))
                    {
                        _absorbShieldManager = new AbsorbShieldManager(log.Source);
                        CreateAbsorbTimerInstance(SourceTimer.AbsorbValue, SourceTimer.Ability, log.Source.Id);
                    }
                }
                if (SourceTimer.TriggerType == TimerKeyType.AbsorbShield && wasTriggered == TriggerType.End)
                {
                    if (_activeTimerInstances.Any(t => t.Value.TargetId == targetInfo.Id))
                    {
                        var absorbTimer = _activeTimerInstances.First(t => t.Value.TargetId == targetInfo.Id);
                        absorbTimer.Value.Complete(true);
                    }
                    _absorbShieldManager = null;
                }
                if (wasTriggered == TriggerType.Start &&
                    _activeTimerInstances.All(t => t.Value.TargetId != targetInfo.Id))
                {
                    if (SourceTimer.TriggerType == TimerKeyType.FightDuration)
                    {
                        _isSingleUseTriggerUsed = true;
                    }

                    if (SourceTimer.TriggerType != TimerKeyType.FightDuration)
                    {
                        if (SourceTimer.TriggerType == TimerKeyType.And || SourceTimer.TriggerType == TimerKeyType.Or)
                        {
                            if (_activeTimerInstances.Count == 0)
                                CreateTimerInstance(log.TimeStamp, targetInfo.Name, targetInfo.Id);
                        }
                        else
                            CreateTimerInstance(log.TimeStamp, targetInfo.Name, targetInfo.Id);
                    }
                }

                if (wasTriggered == TriggerType.End)
                {
                    var endedTimer = _activeTimerInstances.FirstOrDefault(t => t.Value.TargetId == targetInfo.Id).Value;

                    if (endedTimer == null)
                    {
                        return;
                    }

                    if (log.TimeStamp - endedTimer.StartTime < TimeSpan.FromSeconds(1))
                    {
                        return;
                    }
                    endedTimer.Complete(true, true);
                }

                if (log.Effect.EffectType == EffectType.ModifyCharges || (log.Effect.EffectType == EffectType.Apply &&
                    log.Effect.EffectId != _7_0LogParsing._damageEffectId && log.Effect.EffectId != _7_0LogParsing._healEffectId && (int)log.Value.DblValue != 0))
                {
                    UpdateCharges(log, targetInfo);
                }
            }

        }

        private void UpdateCharges(ParsedLogEntry log, TimerTargetInfo targetInfo)
        {
            var timerToUpdate = _activeTimerInstances.FirstOrDefault(t =>
                t.Value.TargetId == targetInfo.Id && (t.Value.SourceTimer.Effect == log.Effect.EffectName || t.Value.SourceTimer.Effect == log.Effect.EffectId.ToString())).Value;
            if (timerToUpdate == null)
                return;
            if (log.Effect.EffectId == 985226842996736 && log.Effect.EffectType == EffectType.Apply)
                timerToUpdate.Charges = 7;
            else
                timerToUpdate.Charges = (int)log.Value.DblValue;
        }
        private TimerTargetInfo GetTargetInfo(ParsedLogEntry log, Timer sourceTimer, TriggerType triggerType, Entity currentTarget)
        {
            if (triggerType == TriggerType.None && log.Effect.EffectType != EffectType.ModifyCharges)
                return new TimerTargetInfo();
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
                var newEntityInfo = TriggerDetection.GetTargetId(log, SourceTimer.Source, currentTarget);
                return new TimerTargetInfo()
                {
                    Name = newEntityInfo.Name,
                    Id = newEntityInfo.Id
                };
            }
            if (sourceTimer.TriggerType == TimerKeyType.EntityHP && triggerType != TriggerType.None)
            {
                var hpTargetInfo = TriggerDetection.GetTargetId(log, SourceTimer.Target,
                    currentTarget);
                return new TimerTargetInfo()
                {
                    Name = hpTargetInfo.Name,
                    Id = hpTargetInfo.Id
                };
            }
            if ((triggerType == TriggerType.Refresh && SourceTimer.CanBeRefreshed) || (triggerType == TriggerType.Start && (SourceTimer.TriggerType == TimerKeyType.And || SourceTimer.TriggerType == TimerKeyType.Or)))
            {
                var sourceTarget =
                    CombatLogStateBuilder.CurrentState.GetPlayerTargetAtTime(log.Source, log.TimeStamp).Entity;
                if (string.IsNullOrEmpty(currentTarget.Name))
                    return new TimerTargetInfo()
                    {
                        Name = sourceTarget.Name,
                        Id = sourceTarget.Id
                    };
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
                if (CheckEncounterAndBoss(this, currentEnecounter) && _activeTimerInstances.Count == 0)
                {
                    CreateTimerNoTarget(combatStart);
                }
            }
            _currentBossInfo = bossInfoParts;
        }
        private void CreateTimerNoTarget(DateTime startTime)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = startTime;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            lock (_timerChangeLock)
            {
                _activeTimerInstances[timerVM.TimerId] = timerVM;
            }
            timerVM.TriggerTimeTimer(startTime);
            Triggered.InvokeSafely();
            NewTimerInstance.InvokeSafely(timerVM);
        }
        public void CreateTimerInstance(DateTime startTime, string targetAdendum, long targetId, int charges = 0)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.StartTime = startTime;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            lock (_timerChangeLock)
            {
                _activeTimerInstances[timerVM.TimerId] = timerVM;
            }
            timerVM.TriggerTimeTimer(startTime);
            if (charges != 0)
                timerVM.Charges = charges;
            Triggered.InvokeSafely();
            NewTimerInstance.InvokeSafely(timerVM);
        }
        private void CreateHPTimerInstance(double currentHP, string targetAdendum, long targetId)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TargetAddendem = targetAdendum;
            timerVM.TargetId = targetId;
            lock (_timerChangeLock)
            {
                _activeTimerInstances[timerVM.TimerId] = timerVM;
            }
            timerVM.TriggerHPTimer(currentHP);
            Triggered.InvokeSafely();
            NewTimerInstance.InvokeSafely(timerVM);
        }

        private void CreateAbsorbTimerInstance(double maxAbsorb, string abilityName, long targetId)
        {
            var timerVM = new TimerInstanceViewModel(SourceTimer);
            timerVM.TimerExpired += CompleteTimer;
            timerVM.TimerId = Guid.NewGuid();
            timerVM.TargetAddendem = abilityName;
            timerVM.TargetId = targetId;
            lock (_timerChangeLock)
            {
                _activeTimerInstances[timerVM.TimerId] = timerVM;
            }
            timerVM.TriggerAbsorbTimer(maxAbsorb);
            Triggered.InvokeSafely();
            NewTimerInstance.InvokeSafely(timerVM);
        }
    }
}
