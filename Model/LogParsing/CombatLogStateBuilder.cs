using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Alerts;
using SWTORCombatParser.Model.CombatParsing;
using System;
using System.Collections.Concurrent;
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
        public static void ResetCombatSpecific()
        {
            CurrentState.PlayerClasses = new ConcurrentDictionary<Entity, SWTORClass>();
            CurrentState.CurrentCharacterPositions = new Dictionary<Entity, PositionData>();
        }

        private static object stateLock = new object();
        public static LogState UpdateCurrentStateWithSingleLog(ParsedLogEntry log, bool liveLog)
        {
            lock (stateLock)
            {
                CurrentState.RawLogs.Add(log);
                if (log.Effect.EffectType == EffectType.AreaEntered)
                {
                    CurrentState.CurrentLocation = log.Effect.EffectName;
                }
                if (log.Effect.EffectType == EffectType.Event && (log.Effect.EffectName == "EnterCombat"))
                {
                    ResetCombatSpecific();
                }

                SetCharacterPositions(log);
                if(liveLog)
                    OutrangedHealerAlert.CheckForOutrangingHealers();
                if(log.Effect.EffectType == EffectType.DisciplineChanged)
                    SetPlayerClass(log);

                UpdateCombatModifierState(log);
                return CurrentState;
            }
        }

        private static void SetCharacterPositions(ParsedLogEntry log)
        {
            if (!string.IsNullOrEmpty(log.Target.Name))
                CurrentState.CurrentCharacterPositions[log.Target] = log.TargetInfo.Position;
            if (!string.IsNullOrEmpty(log.Source.Name))
                CurrentState.CurrentCharacterPositions[log.Source] = log.SourceInfo.Position;
        }

        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine)
        {
            lock (CurrentState.modifierLogLock)
            {


                if (parsedLine.Error == ErrorType.IncompleteLine)
                    return;

                if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Apply)
                {
                    if (!CurrentState.Modifiers.Any(m => m.Type == CombatModfierType.GuardedThreatReduced) || CurrentState.Modifiers.Last(m => m.Type == CombatModfierType.GuardedThreatReduced).StopTime != DateTime.MinValue)
                    {
                        CurrentState.Modifiers.Add(new CombatModifier() { Name = "Guarded-Threat", Source = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedThreatReduced });
                    }
                    else
                    {
                        CurrentState.Modifiers.Add(new CombatModifier() { Name = "Guarded-Damage", Source = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.GuardedDamagedRedirected });
                    }

                    return;
                }
                if (parsedLine.Ability == "Guard" && parsedLine.Effect.EffectType == EffectType.Remove)
                {
                    var guardedModifer = CurrentState.Modifiers.LastOrDefault(m => m.Name.Contains("Guarded") && m.StopTime == DateTime.MinValue);
                    if (guardedModifer != null)
                        guardedModifer.StopTime = parsedLine.TimeStamp;
                    return;
                }
                if (parsedLine.Effect.EffectType == EffectType.Apply && (parsedLine.Target.IsCharacter || parsedLine.Target.IsCompanion) && parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal")
                {
                    var effectToStart = CurrentState.Modifiers.LastOrDefault(m => m.Name == parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName));
                    if (effectToStart == null || (effectToStart.StopTime != DateTime.MinValue && effectToStart.StartTime != parsedLine.TimeStamp))
                    {
                        CurrentState.Modifiers.Add(new CombatModifier() { Name = parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName), Source = parsedLine.Source, Target = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
                    }
                }
                if (parsedLine.Effect.EffectType == EffectType.Remove && (parsedLine.Target.IsCharacter || parsedLine.Target.IsCompanion) && parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal")
                {
                    var effectToEnd = CurrentState.Modifiers.LastOrDefault(m => m.Name == parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName));
                    if (effectToEnd != null)
                        effectToEnd.StopTime = parsedLine.TimeStamp;
                }
            }
        }
        private static string AddSecondHalf(string firstHalf, string effectName)
        {
            if (firstHalf == effectName)
                return "";
            else
                return ": " + effectName;
        }
        private static void SetPlayerClass(ParsedLogEntry parsedLine)
        {
            if (!parsedLine.Source.IsCharacter)
                return;
            if (!CurrentState.PlayerClasses.ContainsKey(parsedLine.Source))
                CurrentState.PlayerClasses[parsedLine.Source] = null;

            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            CurrentState.PlayerClasses[parsedLine.Source] = parsedLine.SourceInfo.Class;

        }

    }
}
