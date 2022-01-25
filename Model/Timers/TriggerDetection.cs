using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SWTORCombatParser.ViewModels.Timers
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
        public static Entity GetTargetId(ParsedLogEntry log, string target, bool targetIsLocal)
        {
            if (TargetIsValid(log, target, targetIsLocal))
            {
                return log.Target;
            }
            if (SourceIsValid(log, target, targetIsLocal))
            {
                return log.Source;
            }
            return null;
        }
        public static TriggerType CheckForHP(ParsedLogEntry log, double hPPercentage, double hpDisplayBuffer, string target, bool targetIsLocal)
        {
            if (TargetIsValid(log, target, targetIsLocal))
            {
                var targetHPPercent = (log.TargetInfo.CurrentHP / log.TargetInfo.MaxHP) * 100;
                if (targetHPPercent <= (hPPercentage + hpDisplayBuffer) && targetHPPercent > hPPercentage)
                    return TriggerType.Start;
                if (targetHPPercent <= hPPercentage)
                    return TriggerType.End;
                if (targetHPPercent > (hPPercentage + hpDisplayBuffer))
                    return TriggerType.End;
            }
            if (SourceIsValid(log, target, targetIsLocal))
            {
                var sourceHPPercentage = (log.SourceInfo.CurrentHP / log.SourceInfo.MaxHP) * 100;
                if (sourceHPPercentage <= (hPPercentage + hpDisplayBuffer) && sourceHPPercentage > hPPercentage)
                    return TriggerType.Start;
                if (sourceHPPercentage <= hPPercentage)
                    return TriggerType.End;
                if (sourceHPPercentage > (hPPercentage + hpDisplayBuffer))
                    return TriggerType.End;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectLoss(ParsedLogEntry log, string effect, string target, bool targetIsLocal)
        {
            if (log.Effect.EffectType != EffectType.Remove)
                return TriggerType.None;
            if (TargetIsValid(log, target, targetIsLocal))
            {
                if (log.Effect.EffectName == effect && log.Effect.EffectType == EffectType.Remove)
                    return TriggerType.Start;
                return TriggerType.None;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForEffectGain(ParsedLogEntry log, string effect, List<string> abilitiesThatRefresh, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if(log.Effect.EffectType == EffectType.Event && log.Effect.EffectName == "Death" && TargetIsValid(log, target, targetIsLocal))
            {
                return TriggerType.End;
            }
            if (SourceIsValid(log, source, sourceIsLocal) && TargetIsValid(log, target, targetIsLocal))
            {
                if(log.Effect.EffectName == effect && log.Effect.EffectType == EffectType.Apply)
                    return TriggerType.Start;
                if (abilitiesThatRefresh.Contains(log.Ability) && log.Effect.EffectType == EffectType.Apply && log.Ability != effect)
                    return TriggerType.Refresh;
                if (abilitiesThatRefresh.Contains(log.Ability) && log.Effect.EffectType == EffectType.Event && log.Ability == effect)
                    return TriggerType.Refresh;
                return TriggerType.None;
            }
            return TriggerType.None;
        }

        public static TriggerType CheckForAbilityUse(ParsedLogEntry log, string ability, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if (log.Effect.EffectType != EffectType.Apply)
                return TriggerType.None;
            if(SourceIsValid(log,source,sourceIsLocal) && TargetIsValid(log, target, targetIsLocal))
            {
                if (log.Ability == ability)
                    return TriggerType.Start;
                return TriggerType.None;
            }
            return TriggerType.None;
        }
        public static TriggerType CheckForComabatStart(ParsedLogEntry log)
        {
            if (log.Effect.EffectName == "EnterCombat")
                return TriggerType.Start;
            else
                return TriggerType.None;
        }

        internal static TriggerType CheckForFightDuration(ParsedLogEntry log, double combatTimeElapsed, DateTime startTime)
        {
            Trace.WriteLine("Elapsed: " + (log.TimeStamp - startTime).TotalSeconds + " vs " + combatTimeElapsed);
           
            if ((log.TimeStamp - startTime).TotalSeconds >= combatTimeElapsed)
                return TriggerType.Start;
            else
                return TriggerType.None;
        }

        private static bool SourceIsValid(ParsedLogEntry log, string source, bool sourceIsLocal)
        {
            if (sourceIsLocal && log.Source.IsLocalPlayer)
                return true;
            if (source == "Any")
                return true;
            if (source == log.Source.Name)
                return true;
            return false;
        }
        private static bool TargetIsValid(ParsedLogEntry log, string target, bool targetIsLocal)
        {
            if (targetIsLocal && log.Target.IsLocalPlayer)
                return true;
            if (target == "Any")
                return true;
            if (target == log.Target.Name)
                return true;
            return false;
        }

        internal static TriggerType CheckForTargetChange(ParsedLogEntry log, string source, bool sourceIsLocal, string target, bool targetIsLocal)
        {
            if (log.Effect.EffectType == EffectType.TargetChanged && SourceIsValid(log, source, sourceIsLocal) && TargetIsValid(log, target, targetIsLocal))
            {
                return TriggerType.Start;
            }
            return TriggerType.None;
        }
    }
}