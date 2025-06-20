using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers
{
    public enum TriggerType
    {
        None,
        Start,
        Refresh,
        End
    }
    public static class TriggerDetection
    {
        public static TriggerType CheckForTriggerNoLog(Timer SourceTimer, DateTime startTime, ConcurrentDictionary<string,TimerInstanceViewModel> activeTimers, List<long> alreadyDetectedEntities, Entity currentTarget, bool fromClause1, bool fromClause2)
        {
            DateTime timeStamp = TimeUtility.CorrectedTime;
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.FightDuration:
                    return CheckForFightDuration(timeStamp, SourceTimer.CombatTimeElapsed, startTime);
                case TimerKeyType.HasEffect:
                    return CheckForHasEffect(timeStamp, SourceTimer.Target, SourceTimer.Effect);
                case TimerKeyType.VariableCheck:
                    return CheckForVariable(SourceTimer);
                case TimerKeyType.IsTimerTriggered:
                    return activeTimers.Any(t => t.Key == SourceTimer.SeletedTimerIsActiveId) ? TriggerType.Start : TriggerType.End;
                case TimerKeyType.And:
                case TimerKeyType.Or:
                    return CheckForDualEffect(SourceTimer, null, SourceTimer.TriggerType, startTime, activeTimers, alreadyDetectedEntities, currentTarget, fromClause1, fromClause2);
            }
            return TriggerType.None;
        }
        public static TriggerType CheckForTrigger(ParsedLogEntry log, Timer SourceTimer, DateTime startTime, ConcurrentDictionary<string,TimerInstanceViewModel> activeTimers, Entity currentTarget, List<long> alreadyDetectedEntities = null)
        {
            DateTime timeStamp = log.TimeStamp;
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.CombatStart:
                    return CheckForComabatStart(log);
                case TimerKeyType.AbilityUsed:
                    return CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source,
    SourceTimer.Target, currentTarget);
                case TimerKeyType.AbsorbShield:
                    return CheckForAbsorbShield(log, SourceTimer.Ability, SourceTimer.Source,
    SourceTimer.Target, currentTarget);
                case TimerKeyType.EffectGained:
                    return CheckForEffectGain(log, SourceTimer.Effect,
                        SourceTimer.AbilitiesThatRefresh, SourceTimer.Source, SourceTimer.Target,
                        currentTarget, SourceTimer.ResetOnEffectLoss);
                case TimerKeyType.EffectLost:
                    return CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Source, SourceTimer.Target,
                        currentTarget);
                case TimerKeyType.EntityHP:
                    return CheckForHP(log, SourceTimer.HPPercentage,
                        SourceTimer.HPPercentageUpper, SourceTimer.Target, currentTarget);
                case TimerKeyType.FightDuration:
                    return CheckForFightDuration(timeStamp, SourceTimer.CombatTimeElapsed, startTime);
                case TimerKeyType.TargetChanged:
                    return CheckForTargetChange(log, SourceTimer.Source, SourceTimer.Target, currentTarget);
                case TimerKeyType.DamageTaken:
                    return CheckForDamageTaken(log, SourceTimer.Source, SourceTimer.Target, SourceTimer.Ability, currentTarget);
                case TimerKeyType.HasEffect:
                    return CheckForHasEffect(timeStamp, SourceTimer.Target, SourceTimer.Effect);
                case TimerKeyType.IsFacing:
                    return CheckForFacing(log, SourceTimer.Source,
                        SourceTimer.Target, currentTarget);
                case TimerKeyType.And:
                case TimerKeyType.Or:
                    return CheckForDualEffect(SourceTimer, log, SourceTimer.TriggerType, startTime, activeTimers, alreadyDetectedEntities, currentTarget, false, false);
                case TimerKeyType.NewEntitySpawn:
                    return CheckForEnemySpawn(SourceTimer, log, alreadyDetectedEntities, currentTarget);
                case TimerKeyType.EntityDeath:
                    return CheckForEntityDeath(SourceTimer, log, currentTarget);
                case TimerKeyType.VariableCheck:
                    return CheckForVariable(SourceTimer);
                case TimerKeyType.IsTimerTriggered:
                    return activeTimers.Any(t => t.Key == SourceTimer.SeletedTimerIsActiveId) ? TriggerType.Start : TriggerType.End;
                case TimerKeyType.EffectCharges:
                    return CheckForCharges(log, SourceTimer, SourceTimer.ChargesSetVariable, SourceTimer.ChargesSetVariableName, currentTarget);
            }
            return TriggerType.None;
        }

        private static TriggerType CheckForCharges(ParsedLogEntry log, Timer sourceTimer, bool setVariable, string varaibleToSet, Entity currentTarget)
        {
            if (EntityIsValid(log.Target, sourceTimer.Target, currentTarget) && EntityIsValid(log.Source, sourceTimer.Source, currentTarget))
            {
                if (!DoesEffectMatchOrContain(log, sourceTimer.Effect))
                    return TriggerType.None;
                if (log.Effect.EffectType == EffectType.ModifyCharges || ((log.Effect.EffectType == EffectType.Apply || log.Effect.EffectType == EffectType.Remove) && log.Effect.EffectId != _7_0LogParsing._healEffectId && log.Effect.EffectId != _7_0LogParsing._damageEffectId))
                {
                    if (!setVariable)
                    {
                        switch (sourceTimer.ComparisonAction)
                        {
                            case VariableComparisons.Equals:
                                return log.Value.DblValue == sourceTimer.ComparisonVal ? TriggerType.Start : TriggerType.None;
                            case VariableComparisons.Less:
                                return log.Value.DblValue < sourceTimer.ComparisonVal ? TriggerType.Start : TriggerType.None;
                            case VariableComparisons.Greater:
                                return log.Value.DblValue > sourceTimer.ComparisonVal ? TriggerType.Start : TriggerType.None;
                            case VariableComparisons.Between:
                                return log.Value.DblValue > sourceTimer.ComparisonValMin && log.Value.DblValue < sourceTimer.ComparisonValMax ? TriggerType.Start : TriggerType.None;
                        }
                        return TriggerType.None;

                    }
                    else
                    {
                        if(log.Effect.EffectType == EffectType.Remove)
                        {
                            OrbsVariableManager.SetVariable(varaibleToSet, 0);
                        }
                        else
                        {
                            OrbsVariableManager.SetVariable(varaibleToSet, (int)log.Value.DblValue);
                        }
                        
                    }
                }
            }
            return TriggerType.None;
        }

        private static TriggerType CheckForVariable(Timer sourceTimer)
        {
            var currentValue = OrbsVariableManager.GetValue(sourceTimer.VariableName);
            switch (sourceTimer.ComparisonAction)
            {
                case VariableComparisons.Equals:
                    return currentValue == sourceTimer.ComparisonVal ? TriggerType.Start : TriggerType.None;
                case VariableComparisons.Less:
                    return currentValue < sourceTimer.ComparisonVal ? TriggerType.Start : TriggerType.None;
                case VariableComparisons.Greater:
                    return currentValue > sourceTimer.ComparisonVal ? TriggerType.Start : TriggerType.None;
                case VariableComparisons.Between:
                    return currentValue > sourceTimer.ComparisonValMin && currentValue < sourceTimer.ComparisonValMax ? TriggerType.Start : TriggerType.None;
            }
            return TriggerType.None;
        }

        public static double GetCurrentTargetHPPercent(ParsedLogEntry log, long targetId)
        {
            var value = -100d;
            if (log.Source.Id == targetId)
            {
                value = log.SourceInfo.CurrentHP / (double)log.SourceInfo.MaxHP * 100d;
            }
            if (log.Target.Id == targetId)
            {
                value = log.TargetInfo.CurrentHP / (double)log.TargetInfo.MaxHP * 100d;
            }
            return value;
        }
        public static Entity GetTargetId(ParsedLogEntry log, string target, Entity currentTarget)
        {
            if (EntityIsValid(log.Target, target, currentTarget))
            {
                return log.Target;
            }
            if (EntityIsValid(log.Source, target, currentTarget))
            {
                return log.Source;
            }
            return null;
        }
        public static TriggerType CheckForHP(ParsedLogEntry log, double hPPercentage, double hpPercentageUpper, string target, Entity currentTarget)
        {
            if (EntityIsValid(log.Target, target, currentTarget))
            {
                var targetHPPercent = (log.TargetInfo.CurrentHP / (double)log.TargetInfo.MaxHP) * 100d;
                if (targetHPPercent <= hPPercentage)
                    return TriggerType.End;
                if (targetHPPercent <= hpPercentageUpper && targetHPPercent > hPPercentage)
                    return TriggerType.Start;
            }
            if (EntityIsValid(log.Source, target, currentTarget))
            {
                var sourceHPPercentage = (log.SourceInfo.CurrentHP / (double)log.SourceInfo.MaxHP) * 100d;
                if (sourceHPPercentage <= hPPercentage)
                    return TriggerType.End;
                if (sourceHPPercentage <= hpPercentageUpper && sourceHPPercentage > hPPercentage)
                    return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectLoss(ParsedLogEntry log, string effect, string source, string target, Entity currentTarget)
        {
            if (log.Effect.EffectType != EffectType.Remove)
                return TriggerType.None;
            if (EntityIsValid(log.Target, target, currentTarget) && EntityIsValid(log.Source, source, currentTarget))
            {
                if (DoesEffectMatchOrContain(log, effect) && log.Effect.EffectType == EffectType.Remove)
                    return TriggerType.Start;
                return TriggerType.None;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectGain(ParsedLogEntry log, string effect, List<string> abilitiesThatRefresh, string source, string target, Entity currentTarget, bool resetOnEffectLost)
        {
            if (log.Effect.EffectType == EffectType.Event && log.Effect.EffectId == _7_0LogParsing.DeathCombatId && EntityIsValid(log.Target, target, currentTarget) && resetOnEffectLost)
            {
                return TriggerType.End;
            }
            if (EntityIsValid(log.Source, source, currentTarget) && EntityIsValid(log.Target, target, currentTarget))
            {
                if (log.Effect.EffectType == EffectType.Remove && DoesEffectMatchOrContain(log, effect) && resetOnEffectLost)
                    return TriggerType.End;
                if (DoesEffectMatchOrContain(log, effect) && log.Effect.EffectType == EffectType.Apply)
                {
                    return TriggerType.Start;
                }
                if ((abilitiesThatRefresh.Contains(log.Ability) || abilitiesThatRefresh.Contains(log.AbilityId.ToString())) && log.Effect.EffectType == EffectType.Event && DoesAbilityMatchOrContain(log, effect))
                {
                    return TriggerType.Refresh;
                }
                if ((abilitiesThatRefresh.Contains(log.Ability) || abilitiesThatRefresh.Contains(log.AbilityId.ToString())) && log.Effect.EffectType == EffectType.Apply && (log.Ability != effect && log.AbilityId.ToString() != effect))
                {
                    return TriggerType.Refresh;
                }
                return TriggerType.None;
            }
            return TriggerType.None;
        }
        private static bool DoesEffectMatchOrContain(ParsedLogEntry log, string userProvided)
        {
            if (userProvided.Contains(","))
            {
                var effectList = userProvided.Split(',').Select(a => a.Trim()).ToList();
                if (effectList.Any(effect => log.Effect.EffectName == effect || log.Effect.EffectId.ToString() == effect))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            if (log.Effect.EffectName == userProvided || log.Effect.EffectId.ToString() == userProvided)
            {
                return true;
            }
            return false;
        }
        private static bool DoesAbilityMatchOrContain(ParsedLogEntry log, string userProvided)
        {
            if (userProvided.Contains(","))
            {
                var abilityList = userProvided.Split(',').Select(a => a.Trim()).ToList();
                if (abilityList.Any(ability => log.Ability == ability || log.AbilityId.ToString() == ability))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            if (log.Ability == userProvided || log.AbilityId.ToString() == userProvided)
            {
                return true;
            }
            return false;
        }
        public static TriggerType CheckForAbilityUse(ParsedLogEntry log, string ability, string source, string target, Entity currentTarget)
        {
            if (log.Effect.EffectType != EffectType.Event)
                return TriggerType.None;
            if (EntityIsValid(log.Source, source, currentTarget) && EntityIsValid(log.Target, target, currentTarget))
            {
                if (ability.Contains(","))
                {
                    var abilityList = ability.Split(',').Select(a => a.Trim()).ToList();
                    if (abilityList.Any(ability => log.Ability == ability || log.AbilityId.ToString() == ability))
                    {
                        if (log.Effect.EffectId == _7_0LogParsing.AbilityActivateId)
                            return TriggerType.Start;
                        if (log.Effect.EffectId == _7_0LogParsing.InterruptCombatId)
                            return TriggerType.End;
                    }
                }
                if (log.Ability == ability || log.AbilityId.ToString() == ability)
                {
                    if (log.Effect.EffectId == _7_0LogParsing.AbilityActivateId)
                        return TriggerType.Start;
                    if (log.Effect.EffectId == _7_0LogParsing.InterruptCombatId)
                        return TriggerType.End;
                }
                return TriggerType.None;
            }
            return TriggerType.None;
        }
        public static TriggerType CheckForAbsorbShield(ParsedLogEntry log, string ability, string source, string target, Entity currentTarget)
        {
            if (EntityIsValid(log.Source, source, currentTarget) && EntityIsValid(log.Target, target, currentTarget))
            {
                if (log.Ability == ability || log.AbilityId.ToString() == ability)
                {
                    if (log.Effect.EffectId == _7_0LogParsing.AbilityActivateId)
                        return TriggerType.Start;
                    if (log.Effect.EffectId == _7_0LogParsing.InterruptCombatId)
                        return TriggerType.End;
                }
                if (log.Effect.EffectName == ability || log.Effect.EffectId.ToString() == ability)
                {
                    if (log.Effect.EffectType == EffectType.Apply)
                    {
                        return TriggerType.Start;
                    }
                }
                return TriggerType.None;
            }
            return TriggerType.None;
        }
        public static TriggerType CheckForComabatStart(ParsedLogEntry log)
        {
            if (log.Effect.EffectId == _7_0LogParsing.EnterCombatId)
                return TriggerType.Start;
            else
                return TriggerType.None;
        }

        internal static TriggerType CheckForFightDuration(DateTime timeStamp, double combatTimeElapsed, DateTime startTime)
        {
            if ((timeStamp - startTime).TotalSeconds >= combatTimeElapsed)
                return TriggerType.Start;
            return TriggerType.None;
        }

        private static bool EntityIsValid(Entity entity, string timerValue, Entity currentTarget)
        {
            if (timerValue == "NotLocalPlayer" && !entity.IsLocalPlayer)
                return true;
            if (timerValue == "LocalPlayer" && entity.IsLocalPlayer)
                return true;
            if (timerValue == "Any" || timerValue == "Ignore" || string.IsNullOrEmpty(timerValue))
                return true;
            if (timerValue == "Players" && entity.IsCharacter)
                return true;
            if (timerValue == "NotPlayers" && !entity.IsCharacter)
                return true;
            if (timerValue == "Boss" && entity.IsBoss)
                return true;
            if (timerValue == "NotBoss" && !entity.IsBoss)
                return true;
            if (timerValue == "CurrentTarget" && entity.Id == currentTarget.Id)
                return true;
            if (timerValue == entity.Name || timerValue == entity.LogId.ToString())
                return true;
            return false;
        }
        private static TriggerType CheckForTargetChange(ParsedLogEntry log, string source, string target, Entity currentTarget)
        {
            if (log.Effect.EffectType == EffectType.TargetChanged && EntityIsValid(log.Source, source, currentTarget) && EntityIsValid(log.Target, target, currentTarget) && log.Effect.EffectId == _7_0LogParsing.TargetSetId)
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForDamageTaken(ParsedLogEntry log, string source, string target, string ability, Entity currentTarget)
        {
            if (log.Effect.EffectType == EffectType.Apply && (log.Ability == ability || log.AbilityId.ToString() == ability) && log.Effect.EffectId == _7_0LogParsing._damageEffectId && EntityIsValid(log.Source, source, currentTarget) && EntityIsValid(log.Target, target, currentTarget))
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForHasEffect(DateTime timeStamp, string target, string effectId)
        {
            if(!ulong.TryParse(effectId, out var parsedId))
                return TriggerType.None;
            var effectsActiveOnTarget =
                CombatLogStateBuilder.CurrentState.GetInstancesOfEffectOnEntityAtTime(timeStamp, target,
                    parsedId);
            if (effectsActiveOnTarget != null && effectsActiveOnTarget.Count > 0 && effectsActiveOnTarget.Any(e => e.EffectName == effectId || e.EffectId.ToString() == effectId))
                return TriggerType.Start;
            return TriggerType.End;

        }

        public static TriggerType CheckForFacing(ParsedLogEntry log, string source, string target, Entity currentTarget)
        {
            if (EntityIsValid(log.Source, source, currentTarget) &&
                EntityIsValid(log.Target, target, currentTarget))
            {
                var sourceHeading = log.SourceInfo.Position.Facing;
                var dotProd = (log.SourceInfo.Position.X * log.TargetInfo.Position.X) + (log.SourceInfo.Position.Y * log.TargetInfo.Position.Y);
                var sourceMag = Math.Sqrt(Math.Pow(log.SourceInfo.Position.X, 2) + Math.Pow(log.SourceInfo.Position.Y, 2));
                var targetMag = Math.Sqrt(Math.Pow(log.TargetInfo.Position.X, 2) + Math.Pow(log.TargetInfo.Position.Y, 2));

                var cosVal = dotProd / (sourceMag * targetMag);
                var angleBetweenSourceAndTarget = Math.Acos(cosVal);
                if ((sourceHeading - 10) >= angleBetweenSourceAndTarget ||
                    sourceHeading + 10 <= angleBetweenSourceAndTarget)
                {
                    return TriggerType.Start;
                }
                else
                    return TriggerType.End;
            }

            return TriggerType.None;
        }

        public static TriggerType CheckForDualEffect(Timer sourceTimer, ParsedLogEntry log, TimerKeyType sourceTimerTriggerType, DateTime startTime, ConcurrentDictionary<string,TimerInstanceViewModel> activeTimers, List<long> alreadyDetectedEntities, Entity currentTarget, bool fromClause1, bool fromClause2)
        {
            var clause1State = log != null ? CheckForTrigger(log, sourceTimer.Clause1, startTime, activeTimers, currentTarget, alreadyDetectedEntities) == TriggerType.Start : CheckForTriggerNoLog(sourceTimer.Clause1, startTime, activeTimers, alreadyDetectedEntities, currentTarget, fromClause1, fromClause2) == TriggerType.Start;
            var clause2State = log != null ? CheckForTrigger(log, sourceTimer.Clause2, startTime, activeTimers, currentTarget, alreadyDetectedEntities) == TriggerType.Start : CheckForTriggerNoLog(sourceTimer.Clause2, startTime, activeTimers, alreadyDetectedEntities, currentTarget, fromClause1, fromClause2) == TriggerType.Start;

            if (sourceTimerTriggerType == TimerKeyType.And)
            {
                return (clause1State || fromClause1) && (clause2State || fromClause2) ? TriggerType.Start : TriggerType.None;
            }
            if (sourceTimerTriggerType == TimerKeyType.Or)
            {
                return clause1State || clause2State || (fromClause1 || fromClause2) ? TriggerType.Start : TriggerType.None;
            }

            return TriggerType.None;
        }

        public static TriggerType CheckForEnemySpawn(Timer sourceTimer, ParsedLogEntry log, List<long> detectedEnemies, Entity currentTarget)
        {
            if (EntityIsValid(log.Source, sourceTimer.Source, currentTarget))
            {
                if (!detectedEnemies.Contains(log.Source.Id))
                {
                    return TriggerType.Start;
                }
            }

            if (EntityIsValid(log.Target, sourceTimer.Source, currentTarget))
            {
                if (!detectedEnemies.Contains(log.Target.Id))
                {
                    return TriggerType.Start;
                }
            }

            return TriggerType.None;
        }
        public static TriggerType CheckForEntityDeath(Timer sourceTimer, ParsedLogEntry log, Entity currentTarget)
        {
            if (EntityIsValid(log.Source, sourceTimer.Source, currentTarget))
            {
                if (log.Effect.EffectId == _7_0LogParsing.DeathCombatId)
                {
                    return TriggerType.Start;
                }
            }

            if (EntityIsValid(log.Target, sourceTimer.Source, currentTarget))
            {
                if (log.Effect.EffectId == _7_0LogParsing.DeathCombatId)
                {
                    return TriggerType.Start;
                }
            }

            return TriggerType.None;
        }


    }
}