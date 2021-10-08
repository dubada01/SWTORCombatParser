using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Model.LogParsing
{

    public static class CombatLogStateBuilder
    {
        public static LogState CurrentState { get; set; } = new LogState();

        public static void ClearState()
        {
            CurrentState = new LogState();
        }
        public static LogState UpdateCurrentLogState(ref List<ParsedLogEntry> logs, string logName)
        {
            foreach (var log in logs)
            {
                UpdateCurrentStateWithSingleLog(log, logName);
            }
            return CurrentState;
        }
        public static LogState UpdateCurrentStateWithSingleLog(ParsedLogEntry log, string logName)
        {
            if (CurrentState.MostRecentLogIndex > log.LogLineNumber)
                return CurrentState;
            CurrentState.MostRecentLogIndex = log.LogLineNumber;

            CurrentState.RawLogs.Add(log);

            SetPlayerClass(log, CurrentState);

            UpdateCombatModifierState(log, CurrentState);
            return CurrentState;
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;

            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Apply)
            {
                if(!state.Modifiers.Any(m=>m.Type == CombatModfierType.GuardedThreatReduced) || state.Modifiers.Last(m => m.Type == CombatModfierType.GuardedThreatReduced).StopTime != DateTime.MinValue)
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Threat", Source = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedThreatReduced });
                }
                else
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Damage", Source = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedDamagedRedirected });
                }
                
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Remove)
            {
                var guardedModifer = state.Modifiers.LastOrDefault(m => m.Name.Contains("Guarded") && m.StopTime == DateTime.MinValue);
                if(guardedModifer!=null)
                    guardedModifer.StopTime = parsedLine.TimeStamp;
                return;
            }
            if(parsedLine.Effect.EffectType == EffectType.Apply && (parsedLine.Target.IsCharacter || parsedLine.Target.IsCompanion) && (parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal"))
            {
                var effectToStart = state.Modifiers.LastOrDefault(m => m.Name == parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName));
                if (effectToStart == null || (effectToStart.StopTime!=DateTime.MinValue && effectToStart.StartTime != parsedLine.TimeStamp))
                {
                    state.Modifiers.Add(new CombatModifier() { Name = parsedLine.Ability+ AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName), Source = parsedLine.Source, Target = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
                }
            }
            if (parsedLine.Effect.EffectType == EffectType.Remove && (parsedLine.Target.IsCharacter || parsedLine.Target.IsCompanion) && (parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal"))
            {
                var effectToEnd = state.Modifiers.LastOrDefault(m => m.Name == parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName));
                if(effectToEnd != null)
                    effectToEnd.StopTime = parsedLine.TimeStamp;
            }
        }
        private static string AddSecondHalf(string firstHalf, string effectName)
        {
            if (firstHalf == effectName)
                return "";
            else
                return ": " + effectName;
        }
        private static void SetPlayerClass(ParsedLogEntry parsedLine, LogState state)
        {
            if (!parsedLine.Source.IsCharacter)
                return;
            if (!state.PlayerClasses.ContainsKey(parsedLine.Source))
                state.PlayerClasses[parsedLine.Source] = null;

            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            var swtorClass = ClassIdentifier.IdentifyClass(parsedLine);
            if (swtorClass == null)
                return;
            state.PlayerClasses[parsedLine.Source] = swtorClass;

        }

    }
}
