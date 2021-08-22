using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public static LogState UpdateCurrentLogState(ref List<ParsedLogEntry> logs)
        {
            var combatLogs = logs;
            foreach (var log in combatLogs)
            {
                SetPlayerName(log, CurrentState);
            }
            foreach (var log in combatLogs)
            {
                if (SetPlayerClass(log, CurrentState))
                    break;
            }
            foreach (var log in combatLogs)
            {
                UpdateCombatModifierState(log, CurrentState);
            }
            return CurrentState;
        }
        public static Role GetPlayerRole(List<ParsedLogEntry> logs)
        {
            foreach (var log in logs)
            {
                var role = GetPlayerRole(log);
                if (role != Role.Unknown)
                    return role;
            }
            return Role.Unknown;
        }
        public static string GetPlayerName(List<ParsedLogEntry> logs)
        {
            foreach (var log in logs)
            {
                var name = GetPlayerName(log);
                if (!string.IsNullOrEmpty(name))
                    return name;
            }
            return "Unknown_Player";
        }
        public static LogState GetStateOfRaidingLogs(List<ParsedLogEntry> raidingLogs)
        {
            foreach (var log in raidingLogs)
            {
                UpdateCombatModifierState(log, CurrentState);
                var identifiedClass = ClassIdentifier.IdentifyClass(log);
                if (identifiedClass != null)
                    CurrentState.PlayerClass = identifiedClass;
            }
            return CurrentState;
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "AbilityActivate")
            {
                state.Modifiers.Add(new CombatModifier() { Name = "Guarding", Source = parsedLine.Source.Name, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Guarding });
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "AbilityDeactivate")
            {
                var guardedModifer = state.Modifiers.LastOrDefault(m => m.Name == "Guarding");
                if (guardedModifer != null)
                    guardedModifer.StopTime = parsedLine.TimeStamp;
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Apply && state.PlayerName != parsedLine.Source.Name)
            {
                if(!state.Modifiers.Any(m=>m.Type == CombatModfierType.GuardedThreatReduced) || state.Modifiers.Last(m => m.Type == CombatModfierType.GuardedThreatReduced).StopTime != DateTime.MinValue)
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Threat", Source = parsedLine.Source.Name, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedThreatReduced });
                }
                else
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Damage", Source = parsedLine.Source.Name, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedDamagedRedirected });
                }
                
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Remove && state.PlayerName != parsedLine.Source.Name)
            {
                var guardedModifer = state.Modifiers.LastOrDefault(m => m.Name.Contains("Guarded") && m.StopTime == DateTime.MinValue);
                if(guardedModifer!=null)
                    guardedModifer.StopTime = parsedLine.TimeStamp;
                return;
            }
            if(parsedLine.Effect.EffectType == EffectType.Apply && parsedLine.Target.IsPlayer && (parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal"))
            {
                var effectToStart = state.Modifiers.LastOrDefault(m => m.Name == parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName));
                if (effectToStart == null || effectToStart.StopTime!=DateTime.MinValue)
                {
                    state.Modifiers.Add(new CombatModifier() { Name = parsedLine.Ability+ AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName), Source = parsedLine.Source.Name, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
                }
            }
            if (parsedLine.Effect.EffectType == EffectType.Remove && parsedLine.Target.IsPlayer && (parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal"))
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
            if (parsedLine.Source.Name != state.PlayerName)
                return false;
            var swtorClass = ClassIdentifier.IdentifyClass(parsedLine);
            if (swtorClass == null)
                return false;
            state.PlayerClass = swtorClass;
            return true;
        }
        private static void SetPlayerName(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            if (string.IsNullOrEmpty(state.PlayerName) && parsedLine.Source.Name == parsedLine.Target.Name)
            { 
                state.PlayerName = parsedLine.Target.Name;
            }
            if (!string.IsNullOrEmpty(state.PlayerName))
            {
                if (parsedLine.Target.Name == state.PlayerName)
                    parsedLine.Target.IsPlayer = true;
                if (parsedLine.Source.Name == state.PlayerName)
                    parsedLine.Source.IsPlayer = true;
            }
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
            if (log.Source.Name == log.Target.Name)
                return log.Source.Name;
            else
                return "";
        }
    }
}
