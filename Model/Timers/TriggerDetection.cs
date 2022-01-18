using SWTORCombatParser.DataStructures;
using System;
using System.Diagnostics;

namespace SWTORCombatParser.ViewModels.Timers
{
    public static class TriggerDetection
    {
        public static bool CheckForHP(ParsedLogEntry log, double hPPercentage, string target, bool targetIsLocal)
        {
            if (TargetIsValid(log, target, targetIsLocal))
            {
                return log.TargetInfo.CurrentHP / log.TargetInfo.MaxHP < hPPercentage;
            }
            return false;
        }

        public static bool CheckForEffectLoss(ParsedLogEntry log, string effect, string target, bool targetIsLocal)
        {
            if (log.Effect.EffectType != EffectType.Remove)
                return false;
            if (TargetIsValid(log, target, targetIsLocal))
            {
                return log.Effect.EffectName == effect && log.Effect.EffectType == EffectType.Remove;
            }
            return false;
        }

        public static bool CheckForEffectGain(ParsedLogEntry log, string effect, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if (log.Effect.EffectType != EffectType.Apply)
                return false;
            if (SourceIsValid(log, source, sourceIsLocal) && TargetIsValid(log, target, targetIsLocal))
            {
                return log.Effect.EffectName == effect;
            }
            return false;
        }

        public static bool CheckForAbilityUse(ParsedLogEntry log, string ability, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if (log.Effect.EffectType != EffectType.Apply)
                return false;
            if(SourceIsValid(log,source,sourceIsLocal) && TargetIsValid(log, target, targetIsLocal))
            {
                return log.Ability == ability;
            }
            return false;
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
        public static bool CheckForComabatStart(ParsedLogEntry log)
        {
            return log.Effect.EffectName == "EnterCombat";
        }

        internal static bool CheckForFightDuration(ParsedLogEntry log, double combatTimeElapsed, DateTime startTime)
        {
            Trace.WriteLine("Elapsed: " + (log.TimeStamp - startTime).TotalSeconds + " vs " + combatTimeElapsed);
            return (log.TimeStamp - startTime).TotalSeconds >= combatTimeElapsed;
        }
    }
}