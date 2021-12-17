using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.Alerts;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
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

        private static object stateLock = new object();
        public static LogState UpdateCurrentStateWithSingleLog(ParsedLogEntry log, bool liveLog)
        {
            lock (stateLock)
            {
                CurrentState.RawLogs.Add(log);
                if (log.Effect.EffectType == EffectType.AreaEntered)
                {
                    CurrentState.CurrentLocation = log.Effect.EffectName;
                    CurrentState.LogVersion = LogVersion.NextGen;
                }
                UpdatePlayerDeathState(log);
                SetCharacterPositions(log);
                if(liveLog)
                    OutrangedHealerAlert.CheckForOutrangingHealers(log.TimeStamp);
                if(log.Effect.EffectType == EffectType.DisciplineChanged)
                    UpdatePlayerClassState(log);
                if (log.LogLocation != null)
                    UpdateEncounterEntered(log);

                UpdateCombatModifierState(log);
                return CurrentState;
            }
        }

        private static void UpdateEncounterEntered(ParsedLogEntry log)
        {
            var location = log.LogLocation;
            var knownEncounters = RaidNameLoader.SupportedEncounters;
            if (knownEncounters.Select(r => r.LogName).Any(ln => log.LogLocation.Contains(ln)))
            {
                var raidOfInterest = knownEncounters.First(r => log.LogLocation.Contains(r.LogName));
                raidOfInterest.Difficutly = RaidNameLoader.SupportedRaidDifficulties.FirstOrDefault(f => log.LogLocation.Contains(f));
                var indendedNumberOfPlayers = RaidNameLoader.SupportedNumberOfPlayers.FirstOrDefault(f => log.LogLocation.Contains(f));
                raidOfInterest.NumberOfPlayer = indendedNumberOfPlayers!=null? indendedNumberOfPlayers:"";
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = raidOfInterest;
            }
            else
            {
                var openWorldLocation = ": " + log.LogLocation;

                var openWorldEncounter =  new EncounterInfo { Name = "Open World" + openWorldLocation, LogName = "Open World" };
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = openWorldEncounter;
            }
        }

        private static void UpdatePlayerDeathState(ParsedLogEntry log)
        {
            if (!log.Target.IsCharacter)
                return;
            var player = log.Target;
            if (!CurrentState.PlayerDeathChangeInfo.Keys.Any(k => k.Id == player.Id))
            { 
                CurrentState.PlayerDeathChangeInfo[player] = new Dictionary<DateTime, bool>();
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = false;
            }
            if (log.Effect.EffectName == "Death")
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = true;
            if(log.Effect.EffectName == "Revived")
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = false;
        }
        private static void UpdatePlayerClassState(ParsedLogEntry parsedLine)
        {
            if (!parsedLine.Source.IsCharacter)
                return;
            if (!CurrentState.PlayerClassChangeInfo.ContainsKey(parsedLine.Source))
                CurrentState.PlayerClassChangeInfo[parsedLine.Source] = null;

            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            if (CurrentState.PlayerClassChangeInfo[parsedLine.Source] == null)
            {
                CurrentState.PlayerClassChangeInfo[parsedLine.Source] = new Dictionary<DateTime, SWTORClass>();
            }
            CurrentState.PlayerClassChangeInfo[parsedLine.Source][parsedLine.TimeStamp] = parsedLine.SourceInfo.Class;
        }
        private static void SetCharacterPositions(ParsedLogEntry log)
        {
            if (!string.IsNullOrEmpty(log.Target.Name))
                CurrentState.CurrentCharacterPositions[log.Target] = log.TargetInfo.Position;
            if (!string.IsNullOrEmpty(log.Source.Name))
                CurrentState.CurrentCharacterPositions[log.Source] = log.SourceInfo.Position;
        }
        //private static int numberOf = 0;
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine)
        {
            lock (CurrentState.modifierLogLock)
            {
                if (parsedLine.Error == ErrorType.IncompleteLine)
                    return;
                var effectName = parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName);
                if (parsedLine.Effect.EffectType == EffectType.Apply && (parsedLine.Target.IsCharacter || parsedLine.Target.IsCompanion) && parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal")
                {
                    if (!CurrentState.Modifiers.ContainsKey(effectName))
                    {
                        CurrentState.Modifiers[effectName] = new List<CombatModifier>();
                    }
                    var modsOfType = CurrentState.Modifiers[effectName];
                    var filteredMods = modsOfType.Where(m=> m.Target == parsedLine.Target && m.Source == parsedLine.Source && !m.Complete);
                    var incompleteEffect = filteredMods.FirstOrDefault();
                    if (incompleteEffect != null)
                    {
                        incompleteEffect.StopTime = parsedLine.TimeStamp;
                        incompleteEffect.Complete = true;
                    }
                    modsOfType.Add(new CombatModifier() { Name = effectName, Source = parsedLine.Source, Target = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
                }
                if (parsedLine.Effect.EffectType == EffectType.Remove && (parsedLine.Target.IsCharacter || parsedLine.Target.IsCompanion) && parsedLine.Effect.EffectName != "Damage" && parsedLine.Effect.EffectName != "Heal")
                {
                    if (string.IsNullOrEmpty(parsedLine.Source.Name))
                    {
                        return;
                    }
                    if (!CurrentState.Modifiers.ContainsKey(effectName))
                        return;
                    var filteredMods = CurrentState.Modifiers[effectName].Where(m => m.Target == parsedLine.Target && m.Source == parsedLine.Source && !m.Complete).ToList();

                    var effectToEnd = filteredMods.FirstOrDefault();
                    if (effectToEnd != null)
                    {
                        effectToEnd.StopTime = parsedLine.TimeStamp;
                        effectToEnd.Complete = true;
                    }
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


    }
}
