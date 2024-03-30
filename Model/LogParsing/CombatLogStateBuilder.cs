﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.LogParsing
{

    public static class CombatLogStateBuilder
    {
        public static event Action<Entity, SWTORClass> PlayerDiciplineChanged = delegate { };
        public static event Action<EncounterInfo> AreaEntered = delegate { };
        public static LogState CurrentState { get; set; } = new LogState();

        public static void ClearState()
        {
            CurrentState = new LogState();
        }

        public static LogState UpdateCurrentStateWithSingleLog(ParsedLogEntry log, bool liveLog)
        {

            CurrentState.RawLogs.Add(log);
            if (log.Effect.EffectType == EffectType.AreaEntered)
            {
                log.Source.IsLocalPlayer = true;
                CurrentState.LocalPlayer = log.Source;
                CurrentState.CurrentLocation = log.Effect.EffectName;
                CurrentState.LogVersion = LogVersion.NextGen;
            }
            UpdatePlayerDeathState(log);
            SetCharacterPositions(log);

            if (log.Effect.EffectType == EffectType.DisciplineChanged)
                UpdatePlayerClassState(log, liveLog);
            if (log.Effect.EffectType == EffectType.TargetChanged)
                UpdatePlayerTargets(log);
            if (log.LogLocation != null)
                UpdateEncounterEntered(log, liveLog);
            UpdateCombatModifierState(log);
            return CurrentState;

        }
        private static void UpdateEncounterEntered(ParsedLogEntry log, bool liveLog)
        {
            var knownEncounters = EncounterLoader.SupportedEncounters.Select(EncounterInfo.GetCopy);
            var encounterInfos = knownEncounters.ToList();
            if (encounterInfos.Select(r => r.LogName).Any(ln => log.LogLocation.Contains(ln)) || encounterInfos.Select(r => r.LogId).Any(ln => log.LogLocationId == ln && !string.IsNullOrEmpty(ln)))
            {
                var raidOfInterest = encounterInfos.First(r => log.LogLocation.Contains(r.LogName) || log.LogLocationId == r.LogId);
                if (!raidOfInterest.IsPvpEncounter)
                {
                    var intendedDifficulty = EncounterLoader.GetLeaderboardFriendlyDifficulty(log.LogDifficultyId);
                    if (string.IsNullOrEmpty(intendedDifficulty))
                        return;
                    raidOfInterest.Difficutly = intendedDifficulty;
                    var indendedNumberOfPlayers = EncounterLoader.GetLeaderboardFriendlyPlayers(log.LogDifficultyId);
                    raidOfInterest.NumberOfPlayer = string.IsNullOrEmpty(indendedNumberOfPlayers) ? "4" : indendedNumberOfPlayers;
                }
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = raidOfInterest;
                CurrentState.CacheEncounterEnterList();
                if (liveLog)
                {
                    AreaEntered(raidOfInterest);
                    if (raidOfInterest.IsPvpEncounter)
                        EncounterTimerTrigger.FirePvpEncounterDetected();
                    else
                        EncounterTimerTrigger.FireNonPvpEncounterDetected();
                }


            }
            else
            {
                var openWorldLocation = ": " + log.LogLocation;

                var openWorldEncounter = new EncounterInfo { Name = "Open World" + openWorldLocation, LogName = "Open World" };
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = openWorldEncounter;
                CurrentState.CacheEncounterEnterList();
                if (liveLog)
                {
                    AreaEntered(openWorldEncounter);
                    EncounterTimerTrigger.FireNonPvpEncounterDetected();
                }

            }
        }
        private static void UpdatePlayerDeathState(ParsedLogEntry log)
        {
            if (!log.Target.IsCharacter)
                return;
            var player = log.Target;
            if (CurrentState.PlayerDeathChangeInfo.Keys.All(k => k.Id != player.Id))
            {
                CurrentState.PlayerDeathChangeInfo[player] = new Dictionary<DateTime, bool>
                {
                    [log.TimeStamp] = false
                };
            }
            if (log.Effect.EffectId == _7_0LogParsing.DeathCombatId)
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = true;
            if (log.Effect.EffectId == _7_0LogParsing.RevivedCombatId)
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = false;
        }
        private static void UpdatePlayerClassState(ParsedLogEntry parsedLine, bool realTime)
        {
            if (!parsedLine.Source.IsCharacter)
                return;
            if (!CurrentState.PlayerClassChangeInfo.ContainsKey(parsedLine.Source))
                CurrentState.PlayerClassChangeInfo[parsedLine.Source] = new Dictionary<DateTime, SWTORClass>();

            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            CurrentState.PlayerClassChangeInfo[parsedLine.Source][parsedLine.TimeStamp] = parsedLine.SourceInfo.Class;
            if (parsedLine.Source.IsLocalPlayer && realTime)
                PlayerDiciplineChanged(parsedLine.Source, parsedLine.SourceInfo.Class);
        }
        private static void UpdatePlayerTargets(ParsedLogEntry log)
        {
            if (!log.Source.IsCharacter)
                return;
            if (!CurrentState.PlayerTargetsInfo.ContainsKey(log.Source))
                CurrentState.PlayerTargetsInfo[log.Source] = new Dictionary<DateTime, EntityInfo>();
            if (log.Error == ErrorType.IncompleteLine)
                return;
            if (log.Effect.EffectId == _7_0LogParsing.TargetSetId)
                CurrentState.PlayerTargetsInfo[log.Source][log.TimeStamp] = log.TargetInfo;
            if (log.Effect.EffectId == _7_0LogParsing.TargetClearedId)
                CurrentState.PlayerTargetsInfo[log.Source][log.TimeStamp] = new EntityInfo();
        }
        private static void SetCharacterPositions(ParsedLogEntry log)
        {
            if (!string.IsNullOrEmpty(log.Target.Name))
                CurrentState.CurrentCharacterPositions[log.Target] = log.TargetInfo.Position;
            if (!string.IsNullOrEmpty(log.Source.Name))
                CurrentState.CurrentCharacterPositions[log.Source] = log.SourceInfo.Position;
        }
        private static CombatModifier GetModifierInScope(Dictionary<Guid, CombatModifier> mods, ParsedLogEntry parsedLine)
        {
            // Store the parsedLine properties in local variables if they are used multiple times.
            var parsedTarget = parsedLine.Target;
            var parsedSource = parsedLine.Source;
            var isSourceNameEmpty = string.IsNullOrEmpty(parsedSource.Name);

            foreach (var kvp in mods)
            {
                var mod = kvp.Value;

                // Now we only have one if-statement with all the conditions.
                if (!mod.Complete &&
                    mod.Target == parsedTarget &&
                    (mod.Source == parsedSource || isSourceNameEmpty))
                {
                    return mod;
                }
            }

            // If no modifier is found that matches the criteria, return null.
            return null;
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine)
        {
            if (parsedLine.Error == ErrorType.IncompleteLine ||
                parsedLine.Effect.EffectType == EffectType.AbsorbShield ||
                parsedLine.Effect.EffectId == _7_0LogParsing._damageEffectId ||
                parsedLine.Effect.EffectId == _7_0LogParsing._healEffectId || parsedLine.Effect.EffectId == null)
                return;
            if (parsedLine.Effect.EffectType != EffectType.Apply &&
                parsedLine.Effect.EffectType != EffectType.Remove &&
                parsedLine.Effect.EffectType != EffectType.ModifyCharges)
                return;
            var effectId = parsedLine.Effect.EffectId;
            if (!CurrentState.Modifiers.ContainsKey(effectId))
            {
                CurrentState.Modifiers[effectId] = new Dictionary<Guid, CombatModifier>();
            }
            var mods = CurrentState.Modifiers[effectId];
            var modifierOfInterest = GetModifierInScope(mods, parsedLine);

            if (parsedLine.Effect.EffectType == EffectType.Apply)
            {
                if (modifierOfInterest != null)
                {
                    modifierOfInterest.StopTime = parsedLine.TimeStamp;
                    modifierOfInterest.Complete = true;
                }

                string koltoShellsId = "985226842996736";
                string traumaProbeId = "999516199190528";
                List<string> longRunningHotIds = new List<string>() { koltoShellsId, traumaProbeId };
                int charges = longRunningHotIds.Contains(effectId) ? 7 : (int)parsedLine.Value.DblValue == 0 ? 1 : (int)parsedLine.Value.DblValue;
                mods[Guid.NewGuid()] = new CombatModifier()
                {
                    Name = parsedLine.ModifierEffectName,
                    EffectName = parsedLine.Effect.EffectName,
                    EffectId = parsedLine.Effect.EffectId,
                    Source = parsedLine.Source,
                    Target = parsedLine.Target,
                    StartTime = parsedLine.TimeStamp,
                    Type = CombatModfierType.Other,
                    ChargesAtTime = new Dictionary<DateTime, int>()
                        {
                            { parsedLine.TimeStamp, charges}
                        }
                };
            }
            if (parsedLine.Effect.EffectType == EffectType.ModifyCharges)
            {
                if (modifierOfInterest != null)
                {
                    modifierOfInterest.ChargesAtTime[parsedLine.TimeStamp] = (int)parsedLine.Value.DblValue;
                }
            }
            if (parsedLine.Effect.EffectType == EffectType.Remove)
            {
                if (modifierOfInterest != null)
                {
                    modifierOfInterest.StopTime = parsedLine.TimeStamp;
                    modifierOfInterest.Complete = true;
                }
            }

        }

    }

}
