using SWTORCombatParser.DataStructures;
using System;


namespace SWTORCombatParser.ViewModels.Timers
{
    public static class TriggerDetection
    {
        //public static bool CheckForTimerExperiation(ParsedLogEntry log, Timer experiationTrigger)
        //{
        //    return experiationTrigger.trig
        //}
        public static bool CheckForHP(ParsedLogEntry log, double hPPercentage, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if (source != null)
                return log.Source.Name == source && (log.SourceInfo.CurrentHP/log.SourceInfo.MaxHP < hPPercentage);
            if (target != null)
                return log.Target.Name == target && (log.TargetInfo.CurrentHP / log.TargetInfo.MaxHP < hPPercentage);
            return false;
        }

        public static bool CheckForEffectLoss(ParsedLogEntry log, string effect, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if (source != null)
                return log.Source.Name == source && log.Effect.EffectType == EffectType.Remove && log.Effect.EffectName == effect;
            if(sourceIsLocal)
                return log.Source.IsLocalPlayer && log.Effect.EffectType == EffectType.Remove && log.Effect.EffectName == effect;
            if (targetIsLocal)
                return log.Target.IsLocalPlayer && log.Effect.EffectType == EffectType.Remove && log.Effect.EffectName == effect;
            if (target != null)
                return log.Target.Name == target && log.Effect.EffectType == EffectType.Remove && log.Effect.EffectName == effect;
            return log.Effect.EffectType == EffectType.Remove && log.Effect.EffectName == effect;
        }

        public static bool CheckForEffectGain(ParsedLogEntry log, string effect, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if (source != null)
                return log.Source.Name == source && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == effect;
            if (sourceIsLocal)
                return log.Source.IsLocalPlayer && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == effect;
            if (targetIsLocal)
                return log.Target.IsLocalPlayer && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == effect;
            if (target != null)
                return log.Target.Name == target && log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == effect;
            return log.Effect.EffectType == EffectType.Apply && log.Effect.EffectName == effect;
        }

        public static bool CheckForAbilityUse(ParsedLogEntry log, string ability, string source, string target, bool sourceIsLocal, bool targetIsLocal)
        {
            if(source != null)
                return log.Source.Name == source && log.Ability == ability;
            if (sourceIsLocal)
                return log.Source.IsLocalPlayer && log.Ability == ability;
            if (targetIsLocal)
                return log.Target.IsLocalPlayer && log.Ability == ability;
            if (target != null)
                return log.Target.Name == target && log.Ability == ability;
            return log.Ability == ability;
        }

        public static bool CheckForComabatStart(ParsedLogEntry log)
        {
            return log.Effect.EffectName == "EnterCombat";
        }
    }
}