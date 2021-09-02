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
        public static LogState GetLocalPlayerClassandName()
        {
            var localState =  new LogState();
            var localLogEntries = CombatLogParser.ParseLast10Mins(CombatLogLoader.LoadMostRecentLog());
            localState.PlayerName = GetPlayerName(localLogEntries);
            foreach (var log in localLogEntries)
            {
                if (SetPlayerClass(log, localState))
                    break;
            }
            return localState;
        }
        public static LogState GetLocalState()
        {
            var directoryInfo = new DirectoryInfo(CombatLogLoader.GetLogDirectory());
            var combatFileNames = directoryInfo.EnumerateFiles().Select(f=>f.Name).ToList();
            foreach(var key in CurrentStates.Keys)
            {
                if (combatFileNames.Contains(key))
                    return CurrentStates[key];
            }
            return new LogState();
        }
        public static void AddParticipant(string log)
        {
            CurrentStates[log] = new LogState();
        }
        public static LogState UpdateCurrentLogState(ref List<ParsedLogEntry> logs, string logName)
        {
            //if (!CurrentStates.ContainsKey(logName))
            //{
            //    CurrentStates[logName] = new LogState();
            //}
            //var state = CurrentStates[logName];
            //var combatLogs = logs;
            //var currentLogs = state.RawLogs;
            //var newLogs = combatLogs.Where(l => !currentLogs.Any(cl=>cl.LogLineNumber == l.LogLineNumber)).ToList();
            //state.RawLogs.AddRange(newLogs);
            //foreach (var log in newLogs)
            //{
            //    SetPlayerName(log, state);
            //}
            //foreach (var log in newLogs)
            //{
            //    if (SetPlayerClass(log, state))
            //        break;
            //}
            //foreach (var log in newLogs)
            //{
            //    UpdateCombatModifierState(log, state);
            //}
            foreach(var log in logs)
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

            SetPlayerName(log, state);

            SetPlayerClass(log, state);

            UpdateCombatModifierState(log, state);
            return state;
        }
        public static Role GetPlayerRole(List<ParsedLogEntry> logs)
        {
            foreach (var log in logs.Where(l=>l.Source.IsPlayer))
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
        public static LogState GetStateOfRaidingLogs(List<ParsedLogEntry> raidingLogs, string logName)
        {
            if (!CurrentStates.ContainsKey(logName))
            {
                CurrentStates[logName] = new LogState();
            }
            foreach (var log in raidingLogs)
            {
                UpdateCombatModifierState(log, CurrentStates[logName]);
                var identifiedClass = ClassIdentifier.IdentifyClass(log);
                if (identifiedClass != null)
                    CurrentStates[logName].PlayerClass = identifiedClass;
            }
            return CurrentStates[logName];
        }
        public static void ClearModifiersExceptGuard(string logName)
        {
            var state = CurrentStates[logName];
            state.Modifiers.RemoveAll(m => m.Type != CombatModfierType.GuardedThreatReduced);
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "AbilityActivate")
            {
                state.Modifiers.Add(new CombatModifier() { Name = "Guarding", Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Guarding });
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
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Threat", Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedThreatReduced });
                }
                else
                {
                    state.Modifiers.Add(new CombatModifier() { Name = "Guarded-Damage", Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedDamagedRedirected });
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
                if (effectToStart == null || (effectToStart.StopTime!=DateTime.MinValue && effectToStart.StartTime != parsedLine.TimeStamp))
                {
                    state.Modifiers.Add(new CombatModifier() { Name = parsedLine.Ability+ AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName), Source = parsedLine.Source, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
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
            if (log.Source.Name == log.Target.Name && log.Source.IsPlayer)
                return log.Source.Name;
            else
                return "";
        }
    }
}
