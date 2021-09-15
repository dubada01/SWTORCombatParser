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
        public static Dictionary<string,LogState> CurrentStates { get; set; } = new Dictionary<string, LogState>();
        public static void ClearState()
        {
            CurrentStates = new Dictionary<string, LogState>();
        }
        public static LogState UpdateCurrentLogState(ref List<ParsedLogEntry> logs, string logName)
        {
            if (!CurrentStates.ContainsKey(logName))
            {
                CurrentStates[logName] = new LogState();
            }
            foreach (var log in logs)
            {
                UpdateCurrentStateWithSingleLog(log, logName);
            }
            return CurrentStates[logName];
        }
        public static LogState UpdateCurrentStateWithSingleLog(ParsedLogEntry log, string logName)
        {
            if (!CurrentStates.ContainsKey(logName))
            {
                CurrentStates[logName] = new LogState();
            }
            var state = CurrentStates[logName];

            if (state.MostRecentLogIndex > log.LogLineNumber)
                return state;
            state.MostRecentLogIndex = log.LogLineNumber;
            //var newLogs = combatLogs.Where(l => !currentLogs.Any(cl => cl.LogLineNumber == l.LogLineNumber)).ToList();
            state.RawLogs.Add(log);

            SetPlayerClass(log, state);

            UpdateCombatModifierState(log, state);
            return state;
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;

            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Apply)
            {
                if(!state.Modifiers.Any(m=>m.Type == CombatModfierType.GuardedThreatReduced) || state.Modifiers.Last(m => m.Type == CombatModfierType.GuardedThreatReduced).StopTime != DateTime.MinValue)
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Threat", Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedThreatReduced });
                }
                else
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Damage", Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedDamagedRedirected });
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
                    state.Modifiers.Add(new CombatModifier() { Name = parsedLine.Ability+ AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName), Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
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
        private static bool SetPlayerClass(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return false;
            var swtorClass = ClassIdentifier.IdentifyClass(parsedLine);
            if (swtorClass == null)
                return false;
            state.PlayerClasses[parsedLine.Source] = swtorClass;
            return true;
        }
        private static Role GetPlayerRole(ParsedLogEntry log)
        {
            if (log.Error == ErrorType.IncompleteLine)
                return Role.Unknown;
            var swtorClass = ClassIdentifier.IdentifyClass(log);
            if (swtorClass == null)
                return Role.Unknown;
            return swtorClass.Role;
        }
        private static string GetPlayerName(ParsedLogEntry log)
        {
            if (log.Source.Name == log.Target.Name && log.Source.IsPlayer)
                return log.Source.Name;
            else
                return "";
        }
    }
}
