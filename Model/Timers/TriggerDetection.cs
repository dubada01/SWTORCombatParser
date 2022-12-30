using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

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
            if (TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                return log.Target;
            }
            if (SourceIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                return log.Source;
            }
            return null;
        }
        public static TriggerType CheckForHP(ParsedLogEntry log, double hPPercentage, double hpDisplayBuffer, string target, bool targetIsLocal, bool targetIsAnyButLocal)
        {
            if (TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                var targetHPPercent = (log.TargetInfo.CurrentHP / log.TargetInfo.MaxHP) * 100;
                if (targetHPPercent <= hPPercentage)
                    return TriggerType.End;
                if (targetHPPercent > (hPPercentage + hpDisplayBuffer))
                    return TriggerType.End;
                if (targetHPPercent <= (hPPercentage + hpDisplayBuffer) && targetHPPercent > hPPercentage)
                    return TriggerType.Start;
            }
            if (SourceIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
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

        public static TriggerType CheckForEffectLoss(ParsedLogEntry log, string effect, string target, bool targetIsLocal,bool sourceIsAnyButLocal, bool targetIsAnyButLocal)
        {
            if (log.Effect.EffectType != EffectType.Remove)
                return TriggerType.None;
            if (TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                if ((log.Effect.EffectName == effect || log.Effect.EffectId == effect) && log.Effect.EffectType == EffectType.Remove)
                    return TriggerType.Start;
                return TriggerType.None;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectGain(ParsedLogEntry log, string effect, List<string> abilitiesThatRefresh, string source, string target, bool sourceIsLocal, bool targetIsLocal,bool sourceIsAnyButLocal, bool targetIsAnyButLocal)
        {
            if(log.Effect.EffectType == EffectType.Event && log.Effect.EffectId == _7_0LogParsing.DeathCombatId && TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                return TriggerType.End;
            }
            if(log.Effect.EffectType == EffectType.Remove && (log.Effect.EffectName == effect || log.Effect.EffectId == effect) && SourceIsValid(log, source, sourceIsLocal,sourceIsAnyButLocal))
            {
                return TriggerType.End;
            }
            if (SourceIsValid(log, source, sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
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
            if (log.Effect.EffectType != EffectType.Event && log.Effect.EffectId != _7_0LogParsing.AbilityActivateId)
                return TriggerType.None;
            if(SourceIsValid(log,source,sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                if (log.Ability == ability || log.AbilityId == ability)
                    return TriggerType.Start;
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

        private static bool SourceIsValid(ParsedLogEntry log, string source, bool sourceIsLocal, bool sourceIsAnyButLocal)
        {
            if (sourceIsAnyButLocal && !log.Source.IsLocalPlayer)
                return true;
            if (sourceIsLocal && log.Source.IsLocalPlayer)
                return true;
            if (source == "Any")
                return true;
            if (source == log.Source.Name || source == log.Source.LogId.ToString())
                return true;
            return false;
        }
        private static bool TargetIsValid(ParsedLogEntry log, string target, bool targetIsLocal, bool targetIsAnyButLocal)
        {
            if (targetIsAnyButLocal && !log.Target.IsLocalPlayer)
                return true;
            if (targetIsLocal && log.Target.IsLocalPlayer)
                return true;
            if (target == "Any")
                return true;
            if (target == log.Target.Name || target == log.Target.LogId.ToString())
                return true;
            return false;
        }

        internal static TriggerType CheckForTargetChange(ParsedLogEntry log, string source, bool sourceIsLocal, string target, bool targetIsLocal, bool targetIsAnyButLocal, bool sourceIsAnyButLocal)
        {
            if (log.Effect.EffectType == EffectType.TargetChanged && SourceIsValid(log, source, sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal) && log.Effect.EffectId == _7_0LogParsing.TargetSetId)
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForDamageTaken(ParsedLogEntry log,  string source, bool sourceIsLocal, string target, bool targetIsLocal, bool targetIsAnyButLocal, bool sourceIsAnyButLocal, string ability)
        {
            if (log.Effect.EffectType == EffectType.Apply && (log.Ability == ability || log.AbilityId == ability) && log.Effect.EffectId == _7_0LogParsing._damageEffectId && SourceIsValid(log, source, sourceIsLocal,sourceIsAnyButLocal) && TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForHasEffect(ParsedLogEntry log,string target, bool targetIsLocal, bool targetIsAnyButLocal, string sourceTimerEffect)
        {
            if (TargetIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {
                var effectsActiveOnTarget =
                    CombatLogStateBuilder.CurrentState.IsEffectOnPlayerAtTime(log.TimeStamp, log.Target,
                        sourceTimerEffect);
                if(effectsActiveOnTarget != null && effectsActiveOnTarget.Count > 0 && effectsActiveOnTarget.Any(e=>e.EffectName == sourceTimerEffect))
                    return TriggerType.Start;
                return TriggerType.End;
            }           
            if (SourceIsValid(log, target, targetIsLocal,targetIsAnyButLocal))
            {                var effectsActiveOnSource =
                    CombatLogStateBuilder.CurrentState.IsEffectOnPlayerAtTime(log.TimeStamp, log.Source,
                        sourceTimerEffect);
                if(effectsActiveOnSource != null && effectsActiveOnSource.Count > 0 && effectsActiveOnSource.Any(e=>e.EffectName == sourceTimerEffect))
                    return TriggerType.Start;
                return TriggerType.End;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForDualEffect(Timer sourceTimer, ParsedLogEntry log, TimerKeyType sourceTimerTriggerType, DateTime startTime)
        {
            var condAStatus = CheckForTrigger(log, sourceTimer.Clause1, startTime);
            var condBStatus = CheckForTrigger(log, sourceTimer.Clause2, startTime);
            if (sourceTimerTriggerType == TimerKeyType.And)
            {
                return condAStatus == TriggerType.Start && condBStatus == TriggerType.Start ? TriggerType.Start : TriggerType.End;
            }
            if (sourceTimerTriggerType == TimerKeyType.Or)
            {
                return condAStatus == TriggerType.Start || condBStatus == TriggerType.Start ? TriggerType.Start : TriggerType.End;
            }

            return TriggerType.None;
        }

        private static TriggerType CheckForTrigger(ParsedLogEntry log, Timer SourceTimer, DateTime startTime)
        {
            switch (SourceTimer.TriggerType)
            {
                case TimerKeyType.CombatStart:
                    return CheckForComabatStart(log);
                    break;
                case TimerKeyType.AbilityUsed:
                    return CheckForAbilityUse(log, SourceTimer.Ability, SourceTimer.Source,
                        SourceTimer.Target, SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal,
                        SourceTimer.SourceIsAnyButLocal, SourceTimer.TargetIsAnyButLocal);
                case TimerKeyType.EffectGained:
                    return CheckForEffectGain(log, SourceTimer.Effect,
                        SourceTimer.AbilitiesThatRefresh, SourceTimer.Source, SourceTimer.Target,
                        SourceTimer.SourceIsLocal, SourceTimer.TargetIsLocal, SourceTimer.SourceIsAnyButLocal,
                        SourceTimer.TargetIsAnyButLocal);
                case TimerKeyType.EffectLost:
                   return CheckForEffectLoss(log, SourceTimer.Effect, SourceTimer.Target,
                        SourceTimer.TargetIsLocal, SourceTimer.SourceIsAnyButLocal,
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
                case TimerKeyType.And:
                case TimerKeyType.Or:
                    return CheckForDualEffect(SourceTimer,log, SourceTimer.TriggerType,startTime);
            }
            return TriggerType.None;
        }
    }
}