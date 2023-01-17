using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;

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
        public static TriggerType CheckForTrigger(ParsedLogEntry log, Timer SourceTimer, DateTime startTime,List<TimerInstanceViewModel> activeTimers, List<long> alreadyDetectedEntities = null)
        {
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.CombatStart:
                    return CheckForComabatStart(log);
                case TimerKeyType.AbilityUsed:
                case TimerKeyType.AbsorbShield:
                    return CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source,
                        SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal);
                case TimerKeyType.EffectGained:
                    return CheckForEffectGain(log, SourceTimer.Effect,
                        SourceTimer.AbilitiesThatRefresh, SourceTimer.Source, SourceTimer.Target,
                        SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal, SourceTimer.SourceIsAnyButLocal,
                        SourceTimer.TargetIsAnyButLocal,SourceTimer.ResetOnEffectLoss);
                case TimerKeyType.EffectLost:
                   return CheckForEffectLoss(log, SourceTimer.Effect,SourceTimer.Source, SourceTimer.Target,
                       SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal, SourceTimer.SourceIsAnyButLocal,
                       SourceTimer.TargetIsAnyButLocal);
                case TimerKeyType.EntityHP:
                    return CheckForHP(log, SourceTimer.HPPercentage,
                        SourceTimer.HPPercentageDisplayBuffer, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.TargetIsAnyButLocal);
                case TimerKeyType.FightDuration:
                    return CheckForFightDuration(log, SourceTimer.CombatTimeElapsed, startTime);
                case TimerKeyType.TargetChanged:
                    return CheckForTargetChange(log, SourceTimer.Source,
                        SourceTimer.SourceIsLocal, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal);
                case TimerKeyType.DamageTaken:
                    return CheckForDamageTaken(log, SourceTimer.Source,
                        SourceTimer.SourceIsLocal, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal, SourceTimer.Ability);
                case TimerKeyType.HasEffect:
                    return CheckForHasEffect(log, SourceTimer.Target, SourceTimer.TargetIsLocal,
                        SourceTimer.TargetIsAnyButLocal, SourceTimer.Effect);
                case TimerKeyType.IsFacing:
                    return CheckForFacing(log, SourceTimer.Source, SourceTimer.SourceIsLocal,
                        SourceTimer.Target, SourceTimer.TargetIsLocal, SourceTimer.TargetIsAnyButLocal,
                        SourceTimer.SourceIsAnyButLocal);
                case TimerKeyType.And:
                case TimerKeyType.Or:
                    return CheckForDualEffect(SourceTimer,log, SourceTimer.TriggerType,startTime,activeTimers,alreadyDetectedEntities);
                case TimerKeyType.NewEntitySpawn:
                    return CheckForEnemySpawn(SourceTimer, log, alreadyDetectedEntities);
            }
            return TriggerType.None;
        }
        public static double GetCurrentTargetHPPercent(ParsedLogEntry log, long targetId)
        {
            var value = -100d;
            if(log.Source.Id == targetId)
            {
                value = log.SourceInfo.CurrentHP / log.SourceInfo.MaxHP * 100;
            }
            if(log.Target.Id == targetId)
            {
                value = (log.TargetInfo.CurrentHP / log.TargetInfo.MaxHP) * 100d;
            }
            return value;
        }
        public static Entity GetTargetId(ParsedLogEntry log, string target, bool targetIsLocal, bool targetIsAnyButLocal)
        {
            if (TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                return log.Target;
            }
            if (SourceIsValid(log.Source, target, targetIsLocal,targetIsAnyButLocal))
            {
                return log.Source;
            }
            return null;
        }
        public static TriggerType CheckForHP(ParsedLogEntry log, double hPPercentage, double hpDisplayBuffer, string target, bool targetIsLocal, bool targetIsAnyButLocal)
        {
            if (TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                var targetHPPercent = (log.TargetInfo.CurrentHP / log.TargetInfo.MaxHP) * 100;
                if (targetHPPercent <= hPPercentage)
                    return TriggerType.End;
                if (targetHPPercent > (hPPercentage + hpDisplayBuffer))
                    return TriggerType.End;
                if (targetHPPercent <= (hPPercentage + hpDisplayBuffer) && targetHPPercent > hPPercentage)
                    return TriggerType.Start;
            }
            if (SourceIsValid(log.Source, target, targetIsLocal,targetIsAnyButLocal))
            {
                var sourceHPPercentage = (log.SourceInfo.CurrentHP / log.SourceInfo.MaxHP) * 100;
                if (sourceHPPercentage <= hPPercentage)
                    return TriggerType.End;
                if (sourceHPPercentage > (hPPercentage + hpDisplayBuffer))
                    return TriggerType.End;
                if (sourceHPPercentage <= (hPPercentage + hpDisplayBuffer) && sourceHPPercentage > hPPercentage)
                    return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectLoss(ParsedLogEntry log, string effect,string source, string target, bool sourceIsLocal, bool targetIsLocal,bool sourceIsAnyButLocal, bool targetIsAnyButLocal)
        {
            if (log.Effect.EffectType != EffectType.Remove)
                return TriggerType.None;
            if (TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal) && SourceIsValid(log.Source,source,sourceIsLocal,sourceIsAnyButLocal))
            {
                if ((log.Effect.EffectName == effect || log.Effect.EffectId == effect) && log.Effect.EffectType == EffectType.Remove)
                    return TriggerType.Start;
                return TriggerType.None;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectGain(ParsedLogEntry log, string effect, List<string> abilitiesThatRefresh, string source, string target, bool sourceIsLocal, bool targetIsLocal,bool sourceIsAnyButLocal, bool targetIsAnyButLocal, bool resetOnEffectLost)
        {
            if(log.Effect.EffectType == EffectType.Event && log.Effect.EffectId == _7_0LogParsing.DeathCombatId && TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                return TriggerType.End;
            }
            if (SourceIsValid(log.Source, source, sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                if (log.Effect.EffectType == EffectType.Remove &&
                    (log.Effect.EffectName == effect || log.Effect.EffectId == effect))
                    return TriggerType.End;
                if ((log.Effect.EffectName == effect || log.Effect.EffectId == effect) &&
                    log.Effect.EffectType == EffectType.Apply)
                {
                    return TriggerType.Start;
                }
                if ((abilitiesThatRefresh.Contains(log.Ability) || abilitiesThatRefresh.Contains(log.AbilityId)) && log.Effect.EffectType == EffectType.Event && (log.Ability == effect || log.AbilityId == effect))
                {
                    return TriggerType.Refresh;
                }
                if ((abilitiesThatRefresh.Contains(log.Ability) || abilitiesThatRefresh.Contains(log.AbilityId)) && log.Effect.EffectType == EffectType.Apply && (log.Ability != effect && log.AbilityId != effect))
                {
                    return TriggerType.Refresh;
                }
                return TriggerType.None;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForAbilityUse(ParsedLogEntry log, string ability, string source, string target, bool sourceIsLocal, bool targetIsLocal,bool sourceIsAnyButLocal, bool targetIsAnyButLocal)
        {
            if (log.Effect.EffectType != EffectType.Event)
                return TriggerType.None;
            if(SourceIsValid(log.Source,source,sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                if (log.Ability == ability || log.AbilityId == ability)
                {
                    if(log.Effect.EffectId == _7_0LogParsing.AbilityActivateId)
                        return TriggerType.Start;
                    if (log.Effect.EffectId == _7_0LogParsing.InterruptCombatId)
                        return TriggerType.End;
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

        internal static TriggerType CheckForFightDuration(ParsedLogEntry log, double combatTimeElapsed, DateTime startTime)
        {
            if ((log.TimeStamp - startTime).TotalSeconds >= combatTimeElapsed)
                return TriggerType.Start;
            else
                return TriggerType.None;
        }

        private static bool SourceIsValid(Entity entity, string source, bool sourceIsLocal, bool sourceIsAnyButLocal)
        {
            if (sourceIsAnyButLocal && !entity.IsLocalPlayer)
                return true;
            if (sourceIsLocal && entity.IsLocalPlayer)
                return true;
            if (source == "Any" || source == "Ignore")
                return true;
            if (source == "Players" && entity.IsCharacter)
                return true;
            if (source == entity.Name || source == entity.LogId.ToString())
                return true;
            return false;
        }
        private static bool TargetIsValid(Entity entity, string target, bool targetIsLocal, bool targetIsAnyButLocal)
        {
            if (targetIsAnyButLocal && !entity.IsLocalPlayer)
                return true;
            if (targetIsLocal && entity.IsLocalPlayer)
                return true;
            if (target == "Any" || target == "Ignore")
                return true;
            if (target == "Players" && entity.IsCharacter)
                return true;
            if (target == entity.Name || target == entity.LogId.ToString())
                return true;
            return false;
        }

        private static TriggerType CheckForTargetChange(ParsedLogEntry log, string source, bool sourceIsLocal, string target, bool targetIsLocal, bool targetIsAnyButLocal, bool sourceIsAnyButLocal)
        {
            if (log.Effect.EffectType == EffectType.TargetChanged && SourceIsValid(log.Source, source, sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal) && log.Effect.EffectId == _7_0LogParsing.TargetSetId)
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForDamageTaken(ParsedLogEntry log,  string source, bool sourceIsLocal, string target, bool targetIsLocal, bool targetIsAnyButLocal, bool sourceIsAnyButLocal, string ability)
        {
            if (log.Effect.EffectType == EffectType.Apply && (log.Ability == ability || log.AbilityId == ability) && log.Effect.EffectId == _7_0LogParsing._damageEffectId && SourceIsValid(log.Source, source, sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForHasEffect(ParsedLogEntry log,string target, bool targetIsLocal, bool targetIsAnyButLocal, string sourceTimerEffect)
        {
            if (TargetIsValid(log.Target, target, targetIsLocal,targetIsAnyButLocal))
            {
                var effectsActiveOnTarget =
                    CombatLogStateBuilder.CurrentState.IsEffectOnPlayerAtTime(log.TimeStamp, log.Target,
                        sourceTimerEffect);
                if(effectsActiveOnTarget != null && effectsActiveOnTarget.Count > 0 && effectsActiveOnTarget.Any(e=>e.EffectName == sourceTimerEffect))
                    return TriggerType.Start;
                return TriggerType.End;
            }           
            if (SourceIsValid(log.Source, target, targetIsLocal,targetIsAnyButLocal))
            {                var effectsActiveOnSource =
                    CombatLogStateBuilder.CurrentState.IsEffectOnPlayerAtTime(log.TimeStamp, log.Source,
                        sourceTimerEffect);
                if(effectsActiveOnSource != null && effectsActiveOnSource.Count > 0 && effectsActiveOnSource.Any(e=>e.EffectName == sourceTimerEffect))
                    return TriggerType.Start;
                return TriggerType.End;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForFacing(ParsedLogEntry log, string source, bool sourceIsLocal, string target,
            bool targetIsLocal, bool targetIsAnyButLocal, bool sourceIsAnyButLocal)
        {
            if (SourceIsValid(log.Source, source, sourceIsLocal, sourceIsAnyButLocal) &&
                TargetIsValid(log.Target, target, targetIsLocal, targetIsAnyButLocal))
            {
                var sourceHeading = log.SourceInfo.Position.Facing;
                var dotProd = (log.SourceInfo.Position.X * log.TargetInfo.Position.X) + (log.SourceInfo.Position.Y * log.TargetInfo.Position.Y);
                var sourceMag = Math.Sqrt(Math.Pow(log.SourceInfo.Position.X, 2)+Math.Pow(log.SourceInfo.Position.Y, 2));
                var targetMag = Math.Sqrt(Math.Pow(log.TargetInfo.Position.X, 2)+Math.Pow(log.TargetInfo.Position.Y, 2));

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

        public static TriggerType CheckForDualEffect(Timer sourceTimer, ParsedLogEntry log, TimerKeyType sourceTimerTriggerType, DateTime startTime, List<TimerInstanceViewModel> activeTimers, List<long> alreadyDetectedEntities)
        {
            var durationTriggers = new List<TimerKeyType>
                { TimerKeyType.HasEffect, TimerKeyType.EntityHP, TimerKeyType.IsTimerTriggered,TimerKeyType.TimerExpired };
            var subTimersForTimer = activeTimers.Where(t =>t.SourceTimer.IsSubTimer && t.SourceTimer.ParentTimerId == sourceTimer.Id).Select(t=>t.SourceTimer);
            
            var clause1State = durationTriggers.Contains(sourceTimer.Clause1.TriggerType)
                ? subTimersForTimer.Contains(sourceTimer.Clause1)
                : CheckForTrigger(log, sourceTimer.Clause1, startTime, activeTimers,alreadyDetectedEntities) == TriggerType.Start;
            var clause2State = durationTriggers.Contains(sourceTimer.Clause2.TriggerType)
                ? subTimersForTimer.Contains(sourceTimer.Clause2)
                : CheckForTrigger(log, sourceTimer.Clause2, startTime, activeTimers,alreadyDetectedEntities) == TriggerType.Start;
            
            if (sourceTimerTriggerType == TimerKeyType.And)
            {
                return clause1State && clause2State ? TriggerType.Start : TriggerType.None;
            }
            if (sourceTimerTriggerType == TimerKeyType.Or)
            {
                return clause1State || clause2State ? TriggerType.Start : TriggerType.None;
            }

            return TriggerType.None;
        }

        public static TriggerType CheckForEnemySpawn(Timer sourceTimer, ParsedLogEntry log, List<long> detectedEnemies)
        {
            if (SourceIsValid(log.Source, sourceTimer.Source, sourceTimer.SourceIsLocal,
                    sourceTimer.SourceIsAnyButLocal))
            {
                if (!detectedEnemies.Contains(log.Source.Id))
                {
                    Debug.WriteLine("New Entity "+log.Source.Id);
                    return TriggerType.Start;
                }
            }

            if (TargetIsValid(log.Target, sourceTimer.Source, sourceTimer.SourceIsLocal,
                    sourceTimer.SourceIsAnyButLocal))
            {
                if (!detectedEnemies.Contains(log.Target.Id))
                {
                    Debug.WriteLine("New Entity "+log.Target.Id);
                    return TriggerType.Start;
                }
            }

            return TriggerType.None;
        }
    }
}