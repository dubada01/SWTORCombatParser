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
        public static LogState GetStateDuringLog(ref List<ParsedLogEntry> logs)
        {
            var logState = new LogState();
            var combatLogs = logs;
            foreach (var log in combatLogs)
            {
                SetPlayerName(log,logState);
            }
            foreach (var log in combatLogs)
            {
                SetPlayerClass(log, logState);
            }
            foreach (var log in combatLogs)
            {
                UpdateCombatModifierState(log, logState);
            }
            return logState;
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "AbilityActivate")
            {
                state.Modifiers.Add(new CombatModifier() { Name = "Guarding", StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Guarding });
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Event && parsedLine.Effect.EffectName == "AbilityDeactivate")
            {
                state.Modifiers.Last(m => m.Name == "Guarding").StopTime = parsedLine.TimeStamp;
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Apply && state.PlayerName != parsedLine.Source.Name)
            {
                state.Modifiers.Add(new CombatModifier() { Name = "Guarded", StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Guarded });
                return;
            }
            if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Remove && state.PlayerName != parsedLine.Source.Name)
            {
                state.Modifiers.Last(m => m.Name == "Guarded").StopTime = parsedLine.TimeStamp;
                return;
            }
            if(parsedLine.Effect.EffectType == EffectType.Apply && parsedLine.Target.IsPlayer && (parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal"))
            {
                var effectToStart = state.Modifiers.LastOrDefault(m => m.Name == parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName));
                if (effectToStart == null || effectToStart.StopTime!=DateTime.MinValue)
                {
                    state.Modifiers.Add(new CombatModifier() { Name = parsedLine.Ability+ AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName), StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
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
        private static void SetPlayerClass(ParsedLogEntry parsedLine, LogState state)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            if (state.PlayerClass != null || parsedLine.Source.Name != state.PlayerName)
                return;
            var swtorClass = ClassIdentifier.IdentifyClass(parsedLine);
            if (swtorClass == null)
                return;
            state.PlayerClass = swtorClass;
            //if (_healingDisciplines.Contains(state.PlayerClass.Discipline))
            //    _combatState.HealsPerThreat = 2.2d;
            //if (_tankDisciplines.Contains(state.PlayerClass.Discipline))
            //    _combatState.HealsPerThreat = 0.8d;
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
    }
}
