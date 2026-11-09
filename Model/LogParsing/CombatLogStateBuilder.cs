using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SWTORCombatParser.Utilities;

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
            if (log.Effect.EffectType == EffectType.AreaEntered)
            {
                log.Source.IsLocalPlayer = true;
                CurrentState.LocalPlayer = log.Source;
                CurrentState.CurrentLocation = log.Effect.EffectName;
                CurrentState.LogVersion = LogVersion.NextGen;
            }
            UpdatePlayerDeathState(log);
            UpdateEnemyDeathState(log);
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
            if (encounterInfos.Select(r => r.LogName).Any(ln => log.LogLocation.Contains(ln)) || encounterInfos.Select(r => r.LogId).Any(ln => log.LogLocationId == ln && ln != 0))
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
                    AreaEntered.InvokeSafely(raidOfInterest);
                    if (raidOfInterest.IsPvpEncounter)
                        EncounterTimerTrigger.FirePvpEncounterDetected(log.TimeStamp,raidOfInterest);
                    else
                        EncounterTimerTrigger.FireNonPvpEncounterDetected(log.TimeStamp,raidOfInterest);
                }


            }
            else
            {
                var openWorldEncounter = EncounterInfo.GetCopy(EncounterLoader.SupportedEncounters.First(e=>e.EncounterType == EncounterType.OpenWorld));
                openWorldEncounter.Name = openWorldEncounter.Name + ": " + log.LogLocation;
                CurrentState.EncounterEnteredInfo[log.TimeStamp] = openWorldEncounter;
                CurrentState.CacheEncounterEnterList();
                if (liveLog)
                {
                    AreaEntered.InvokeSafely(openWorldEncounter);
                    EncounterTimerTrigger.FireNonPvpEncounterDetected(log.TimeStamp, openWorldEncounter);
                }

            }
        }
        private static void UpdatePlayerDeathState(ParsedLogEntry log)
        {
            if (!log.Target.IsCharacter)
                return;
            var player = log.Target;
            if (!CurrentState.PlayerDeathChangeInfo.ContainsKey(player) &&
                CurrentState.PlayerDeathChangeInfo.Keys.All(k => k.Id != player.Id))
            {
                CurrentState.PlayerDeathChangeInfo[player] = new ConcurrentDictionary<DateTime, bool>
                {
                    [log.TimeStamp] = false
                };
            }
            if (log.Effect.EffectId == _7_0LogParsing.DeathCombatId)
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = true;
            if (log.Effect.EffectId == _7_0LogParsing.RevivedCombatId)
                CurrentState.PlayerDeathChangeInfo[player][log.TimeStamp] = false;
        }
        private static void UpdateEnemyDeathState(ParsedLogEntry log)
        {
            UpdateEnemyDeathStateForEntity(log.Target, log);
            UpdateEnemyDeathStateForEntity(log.Source, log);
        }

        private static void UpdateEnemyDeathStateForEntity(Entity e, ParsedLogEntry log)
        {
            if (e.IsCharacter)
                return;
            if (!CurrentState.EnemyDeathChangeInfo.ContainsKey(e) &&
                CurrentState.EnemyDeathChangeInfo.Keys.All(k => k.Id != e.Id))
            {
                CurrentState.EnemyDeathChangeInfo[e] = new ConcurrentDictionary<DateTime, bool>
                {
                    [log.TimeStamp] = false
                };
            }
            if (log.Effect.EffectId == _7_0LogParsing.DeathCombatId && log.Target == e)
                CurrentState.EnemyDeathChangeInfo[e][log.TimeStamp] = true;
        }
        private static void UpdatePlayerClassState(ParsedLogEntry parsedLine, bool realTime)
        {
            if (!parsedLine.Source.IsCharacter)
                return;
            if (!CurrentState.PlayerClassChangeInfo.ContainsKey(parsedLine.Source))
                CurrentState.PlayerClassChangeInfo[parsedLine.Source] = new ConcurrentDictionary<DateTime, SWTORClass>();

            if (parsedLine.Error == ErrorType.IncompleteLine)
                return;
            CurrentState.PlayerClassChangeInfo[parsedLine.Source][parsedLine.TimeStamp] = parsedLine.SourceInfo.Class;
            if (parsedLine.Source.IsLocalPlayer && realTime)
                PlayerDiciplineChanged.InvokeSafely(parsedLine.Source, parsedLine.SourceInfo.Class);
        }
        private static void UpdatePlayerTargets(ParsedLogEntry log)
        {
            var dictToUse = CurrentState.PlayerTargetsInfo;
            if (!log.Source.IsCharacter)
            {
                dictToUse = CurrentState.EnemyTargetsInfo;
            }
            if (!dictToUse.ContainsKey(log.Source))
                dictToUse[log.Source] = new ConcurrentDictionary<DateTime, EntityInfo>();
            if (log.Error == ErrorType.IncompleteLine)
                return;
            if (log.Effect.EffectId == _7_0LogParsing.TargetSetId)
                dictToUse[log.Source][log.TimeStamp] = log.TargetInfo;
            if (log.Effect.EffectId == _7_0LogParsing.TargetClearedId)
                dictToUse[log.Source][log.TimeStamp] = new EntityInfo();
        }
        private static void SetCharacterPositions(ParsedLogEntry log)
        {
            if (!string.IsNullOrEmpty(log.Target.Name))
                CurrentState.CurrentCharacterPositions[log.Target] = log.TargetInfo.Position;
            if (!string.IsNullOrEmpty(log.Source.Name))
                CurrentState.CurrentCharacterPositions[log.Source] = log.SourceInfo.Position;
        }
        private static CombatModifier GetModifierInScope(ConcurrentDictionary<Guid, CombatModifier> mods, ParsedLogEntry parsedLine, out Guid modifierId,
            bool matchAnySource = false)
        {
            // Store the parsedLine properties in local variables if they are used multiple times.
            var parsedTarget = parsedLine.Target;
            var parsedSource = parsedLine.Source;
            var isSourceNameEmpty = matchAnySource || string.IsNullOrEmpty(parsedSource.Name);
            CombatModifier closest = null;
            modifierId = default;

            foreach (var kvp in mods)
            {
                var mod = kvp.Value;

                if (!mod.Complete &&
                    mod.Target == parsedTarget &&
                    (mod.Source == parsedSource || isSourceNameEmpty))
                {
                    if (!isSourceNameEmpty)
                    {
                        modifierId = kvp.Key;
                        return mod;
                    }

                    // Missing-source removals/charge changes cannot identify an old
                    // caster instance. Use the latest application on this target, not
                    // random Guid dictionary order, which can close a previous pull's
                    // unremoved effect and make it appear throughout later combats.
                    if (mod.StartTime > parsedLine.TimeStamp)
                        continue;
                    if (closest == null || mod.StartTime > closest.StartTime ||
                        (mod.StartTime == closest.StartTime &&
                         (mod.Source.Id < closest.Source.Id ||
                          (mod.Source.Id == closest.Source.Id && mod.Source.LogId < closest.Source.LogId))))
                    {
                        closest = mod;
                        modifierId = kvp.Key;
                    }
                }
            }

            return closest;
        }
        private static void UpdateCombatModifierState(ParsedLogEntry parsedLine)
        {
            if (CurrentState.UnknownRemovalTimestamp != parsedLine.TimeStamp)
            {
                CurrentState.UnknownSourceRemovals.Clear();
                CurrentState.UnknownRemovalTimestamp = parsedLine.TimeStamp;
            }
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
            if (!CurrentState.Modifiers.TryGetValue(effectId, out var mods))
            {
                mods = new ConcurrentDictionary<Guid, CombatModifier>();
                CurrentState.Modifiers[effectId] = mods;
            }
            if (!CurrentState.ActiveModifiers.TryGetValue(effectId, out var activeMods))
            {
                // Also support a state whose modifier history was populated before its first update.
                activeMods = new ConcurrentDictionary<Guid, CombatModifier>(mods.Where(pair => !pair.Value.Complete));
                CurrentState.ActiveModifiers[effectId] = activeMods;
            }
            var modifierOfInterest = GetModifierInScope(activeMods, parsedLine, out var modifierId);

            if (parsedLine.Effect.EffectType == EffectType.Apply)
            {
                if (modifierOfInterest != null)
                {
                    modifierOfInterest.StopTime = parsedLine.TimeStamp;
                    modifierOfInterest.Complete = true;
                    activeMods.TryRemove(modifierId, out _);
                }

                const ulong koltoShellsId = 985226842996736;
                const ulong traumaProbeId = 999516199190528;
                int charges = effectId == koltoShellsId || effectId == traumaProbeId ? 7 : (int)parsedLine.Value.DblValue == 0 ? 1 : (int)parsedLine.Value.DblValue;
                var newModifier = new CombatModifier()
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
                var newModifierId = Guid.NewGuid();
                mods[newModifierId] = newModifier;
                activeMods[newModifierId] = newModifier;
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
                var unknownSource = string.IsNullOrEmpty(parsedLine.Source.Name);
                if (modifierOfInterest == null && !unknownSource &&
                    CurrentState.UnknownSourceRemovals.TryGetValue(effectId, out var previousUnknownRemovals))
                {
                    var previousIndex = previousUnknownRemovals.FindIndex(modifier =>
                        modifier.Source == parsedLine.Source && modifier.Target == parsedLine.Target);
                    if (previousIndex >= 0)
                    {
                        // An earlier unknown removal provisionally claimed this caster.
                        // The explicit event now owns that closure; reassign the unknown
                        // event to another active instance at this same timestamp.
                        previousUnknownRemovals.RemoveAt(previousIndex);
                        modifierOfInterest = GetModifierInScope(activeMods, parsedLine, out modifierId, matchAnySource: true);
                        unknownSource = true;
                    }
                }
                if (modifierOfInterest != null)
                {
                    modifierOfInterest.StopTime = parsedLine.TimeStamp;
                    modifierOfInterest.Complete = true;
                    activeMods.TryRemove(modifierId, out _);
                    if (unknownSource)
                    {
                        if (!CurrentState.UnknownSourceRemovals.TryGetValue(effectId, out var unknownRemovals))
                        {
                            unknownRemovals = new List<CombatModifier>();
                            CurrentState.UnknownSourceRemovals[effectId] = unknownRemovals;
                        }
                        unknownRemovals.Add(modifierOfInterest);
                    }
                }
            }

        }

    }

}
