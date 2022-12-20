using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser.Model.LogParsing
{

    public static class CombatLogStateBuilder
    {
        public static event Action<Entity, SWTORClass> PlayerDiciplineChanged = delegate { };
        public static event Action AreaEntered = delegate { };
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
                    log.Source.IsLocalPlayer = true;
                    CurrentState.LocalPlayer = log.Source;
                    CurrentState.CurrentLocation = log.Effect.EffectName;
                    CurrentState.LogVersion = LogVersion.NextGen;
                }
                UpdatePlayerDeathState(log);
                SetCharacterPositions(log);

                if(log.Effect.EffectType == EffectType.DisciplineChanged)
                    UpdatePlayerClassState(log,liveLog);
                if(log.Effect.EffectType == EffectType.TargetChanged)
                    UpdatePlayerTargets(log);
                if (log.LogLocation != null)
                    UpdateEncounterEntered(log, liveLog);
                UpdateCombatModifierState(log);
                return CurrentState;
            }
        }
        private static void UpdateEncounterEntered(ParsedLogEntry log, bool liveLog)
        {
            if(liveLog)
                AreaEntered();
            var knownEncounters = EncounterLoader.SupportedEncounters.Select(EncounterInfo.GetCopy);
            var encounterInfos = knownEncounters.ToList();
            if (encounterInfos.Select(r => r.LogName).Any(ln => log.LogLocation.Contains(ln)) || encounterInfos.Select(r=>r.LogId).Any(ln=>log.LogLocationId == ln && !string.IsNullOrEmpty(ln)))
            {
                var raidOfInterest = encounterInfos.First(r => log.LogLocation.Contains(r.LogName) || log.LogLocationId == r.LogId);
                var intendedDifficulty = EncounterLoader.GetLeaderboardFriendlyDifficulty(log.LogLocation);
                raidOfInterest.Difficutly = intendedDifficulty ?? "Story";
                var indendedNumberOfPlayers = EncounterLoader.GetLeaderboardFriendlyPlayers(log.LogLocation);
                raidOfInterest.NumberOfPlayer = indendedNumberOfPlayers ?? "4";
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = raidOfInterest;
                if (liveLog)
                {
                    if(raidOfInterest.IsPvpEncounter)
                        EncounterTimerTrigger.FirePvpEncounterDetected();
                    else
                        EncounterTimerTrigger.FireNonPvpEncounterDetected();
                }
                    

            }
            else
            {
                if (liveLog)
                    EncounterTimerTrigger.FireNonPvpEncounterDetected();
                var openWorldLocation = ": " + log.LogLocation;

                var openWorldEncounter =  new EncounterInfo { Name = "Open World" + openWorldLocation, LogName = "Open World"};
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = openWorldEncounter;
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
            if(log.Effect.EffectId == _7_0LogParsing.RevivedCombatId)
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
            if(parsedLine.Source.IsLocalPlayer && realTime)
                PlayerDiciplineChanged(parsedLine.Source, parsedLine.SourceInfo.Class);
        }
        private static void UpdatePlayerTargets(ParsedLogEntry log)
        {
            if (!log.Source.IsCharacter)
                return;
            if (!CurrentState.PlayerTargetsInfo.ContainsKey(log.Source))
                CurrentState.PlayerTargetsInfo[log.Source] = new Dictionary<DateTime, Entity>();
            if (log.Error == ErrorType.IncompleteLine)
                return;
            if(log.Effect.EffectId == _7_0LogParsing.TargetSetId)
                CurrentState.PlayerTargetsInfo[log.Source][log.TimeStamp] = log.Target;
            if (log.Effect.EffectId == _7_0LogParsing.TargetClearedId)
                CurrentState.PlayerTargetsInfo[log.Source][log.TimeStamp] = Entity.EmptyEntity;
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
            lock (CurrentState.ModifierLogLock)
            {
                if (parsedLine.Error == ErrorType.IncompleteLine)
                    return;
                if (parsedLine.Effect.EffectType == EffectType.AbsorbShield)
                    return;
                var effectName = parsedLine.Ability + AddSecondHalf(parsedLine.Ability, parsedLine.Effect.EffectName);
                if (parsedLine.Effect.EffectType == EffectType.Apply &&  parsedLine.Effect.EffectId != _7_0LogParsing._damageEffectId && parsedLine.Effect.EffectId != _7_0LogParsing._healEffectId)
                {
                    if (!CurrentState.Modifiers.ContainsKey(effectName))
                    {
                        CurrentState.Modifiers[effectName] = new ConcurrentDictionary<Guid, CombatModifier>();
                    }
                    ConcurrentDictionary<Guid, CombatModifier> mods = new ConcurrentDictionary<Guid, CombatModifier>();
                    CurrentState.Modifiers.TryGetValue(effectName, out mods);
                    var filteredMods = mods.Where(m=> m.Value.Target == parsedLine.Target && m.Value.Source == parsedLine.Source && !m.Value.Complete);
                    var incompleteEffect = filteredMods.FirstOrDefault();
                    if (incompleteEffect.Value != null)
                    {
                        incompleteEffect.Value.StopTime = parsedLine.TimeStamp;
                        incompleteEffect.Value.Complete = true;
                    }
                    mods.TryAdd(Guid.NewGuid(),new CombatModifier() { Name = effectName, EffectName = parsedLine.Effect.EffectName, Source = parsedLine.Source, Target = parsedLine.Target, StartTime = parsedLine.TimeStamp, Type = CombatModfierType.Other });
                }
                if (parsedLine.Effect.EffectType == EffectType.Remove && parsedLine.Effect.EffectId != _7_0LogParsing._damageEffectId && parsedLine.Effect.EffectId != _7_0LogParsing._healEffectId)
                {
                    if (string.IsNullOrEmpty(parsedLine.Source.Name))
                    {
                        return;
                    }
                    if (!CurrentState.Modifiers.ContainsKey(effectName))
                        return;
                    var filteredMods = CurrentState.Modifiers[effectName].Where(m => m.Value.Target == parsedLine.Target && m.Value.Source == parsedLine.Source && !m.Value.Complete).ToList();

                    var effectToEnd = filteredMods.FirstOrDefault();
                    if (effectToEnd.Value != null)
                    {
                        effectToEnd.Value.StopTime = parsedLine.TimeStamp;
                        effectToEnd.Value.Complete = true;
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
