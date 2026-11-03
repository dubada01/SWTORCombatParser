//using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.LogParsing
{
    public enum CombatModfierType
    {
        Other,
        GuardedThreatReduced,
        GuardedDamagedRedirected,
        Guarding,
        DefensiveBuff,
        OffensiveBuff,
        Debuff,
        HealerShield
    }
    public enum LogVersion
    {
        Legacy,
        NextGen
    }
    public class CombatModifier
    {
        public bool HasAbsorbBeenCounted { get; set; }
        public string Name { get; set; }
        public string EffectName { get; set; }
        public ulong EffectId { get; set; }
        public CombatModfierType Type { get; set; }
        public Dictionary<DateTime, int> ChargesAtTime { get; set; } = new();
        public int GetEffectStackForTimestamp(DateTime targetTime)
        {
            // Try to get the exact timestamp first
            if (ChargesAtTime.TryGetValue(targetTime, out var value))
            {
                return value;
            }

            // Use reverse enumerator to find the closest key <= targetTime
            foreach (var kvp in ChargesAtTime.Reverse())
            {
                if (kvp.Key <= targetTime)
                {
                    return kvp.Value;
                }
            }

            // No valid timestamp found
            return 0; // Or other default value
        }
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public bool Complete { get; set; }
        public double DurationSeconds => StopTime == DateTime.MinValue ? 0 : (StopTime - StartTime).TotalSeconds;
    }
    public class LogState
    {
        private List<DateTime> _orderedEncounterChangeTimes = new();

        public ConcurrentDictionary<Entity, ConcurrentDictionary<DateTime, SWTORClass>> PlayerClassChangeInfo = new();
        public ConcurrentDictionary<Entity, ConcurrentDictionary<DateTime, EntityInfo>> PlayerTargetsInfo = new();
        public ConcurrentDictionary<Entity, ConcurrentDictionary<DateTime, EntityInfo>> EnemyTargetsInfo = new();
        public Dictionary<Entity, ConcurrentDictionary<DateTime, bool>> PlayerDeathChangeInfo = new();
        public Dictionary<Entity, ConcurrentDictionary<DateTime, bool>> EnemyDeathChangeInfo = new();
        public Dictionary<DateTime, EncounterInfo> EncounterEnteredInfo = new();

        public LogVersion LogVersion { get; set; } = LogVersion.Legacy;
        public string CurrentLocation { get; set; }
        public ConcurrentDictionary<ulong, ConcurrentDictionary<Guid, CombatModifier>> Modifiers { get; set; } = new();
        public Dictionary<Entity, PositionData> CurrentCharacterPositions { get; set; } = new();
        public PositionData CurrentLocalCharacterPosition => LocalPlayer == null ? new PositionData() : CurrentCharacterPositions[LocalPlayer];
        public Entity LocalPlayer { get; internal set; }

        private static object _effectsModLock = new();


        public bool WasPlayerDeadAtTime(Entity player, DateTime timestamp)
        {
            if (!PlayerDeathChangeInfo.TryGetValue(player, out var playerDeathInfo))
            {
                return true;
            }
            if (!playerDeathInfo.Any(d => d.Key < timestamp))
                return true;
            var updateTimes = playerDeathInfo.Keys.ToList();
            for (var i = 0; i < updateTimes.Count; i++)
            {
                if (i == updateTimes.Count - 1)
                {
                    return playerDeathInfo[updateTimes.Last()];
                }
                if (updateTimes[i] == timestamp)
                    return playerDeathInfo[updateTimes[i]];
                if (updateTimes[i] > timestamp && i != 0)
                    return playerDeathInfo[updateTimes[i - 1]];
            }
            return false;
        }
        public bool WasEnemyDeadAtTime(Entity enemy, DateTime timestamp)
        {
            if (!EnemyDeathChangeInfo.TryGetValue(enemy, out var enemyDeathInfo))
            {
                return true;
            }

            if (timestamp > enemyDeathInfo.Max(v => v.Key))
                return enemyDeathInfo.MaxBy(v => v.Key).Value;
            if (!enemyDeathInfo.Any(d => d.Key < timestamp))
                return true;
            var updateTimes = enemyDeathInfo.Keys.ToList();
            for (var i = 0; i < updateTimes.Count; i++)
            {
                if (i == updateTimes.Count - 1)
                {
                    return enemyDeathInfo[updateTimes.Last()];
                }
                if (updateTimes[i] == timestamp)
                    return enemyDeathInfo[updateTimes[i]];
                if (updateTimes[i] > timestamp && i != 0)
                    return enemyDeathInfo[updateTimes[i - 1]];
            }
            return false;
        }
        public bool IsPvpOpponentAtTime(Entity argTarget, DateTime combatStart)
        {
            return !argTarget.IsCompanion && GetCharacterClassAtTime(argTarget, combatStart).Discipline == null;
        }
        public EncounterInfo GetEncounterActiveAtTime(DateTime time)
        {
            if (!_orderedEncounterChangeTimes.Any() || time < _orderedEncounterChangeTimes[0])
                return new EncounterInfo() { Name = "Unknown Encounter" };
            return EncounterEnteredInfo[GetEncounterStartTime(time)];
        }

        private DateTime GetEncounterStartTime(DateTime time)
        {
            if (!_orderedEncounterChangeTimes.Any() || time < _orderedEncounterChangeTimes.First())
                return time;
            if (time > _orderedEncounterChangeTimes.Last())
                return _orderedEncounterChangeTimes.Last();
            for (var i = 0; i < _orderedEncounterChangeTimes.Count; i++)
            {
                if (_orderedEncounterChangeTimes[i] == time)
                    return _orderedEncounterChangeTimes[i];
                if (_orderedEncounterChangeTimes[i] > time)
                    return _orderedEncounterChangeTimes[i - 1];
            }

            return time;
        }
        public SWTORClass GetLocalPlayerClassAtTime(DateTime time)
        {
            if (LocalPlayer == null)
                return new SWTORClass();
            if (PlayerClassChangeInfo.Keys.All(k => k.Id != LocalPlayer.Id))
                return new SWTORClass();
            var classOfSource = PlayerClassChangeInfo[PlayerClassChangeInfo.Keys.First(k => k.Id == LocalPlayer.Id)];
            if (classOfSource == null)
                return new SWTORClass();
            var mostRecentClassChangeTime = classOfSource.Keys.ToList().MinBy(l => Math.Abs((time - l).TotalSeconds));
            var classAtTime = classOfSource[mostRecentClassChangeTime];
            return classAtTime;
        }
        public SWTORClass GetCharacterClassAtTime(Entity entity, DateTime time)
        {
            if (entity == null || !PlayerClassChangeInfo.TryGetValue(entity, out var classOfSource))
                return new SWTORClass();
            if (classOfSource == null)
                return new SWTORClass();

            var mostRecentClassChangeTime = classOfSource.Keys.ToList().MinBy(l => Math.Abs((time - l).TotalSeconds));
            var currentEncounter = GetEncounterActiveAtTime(time);
            var encounterTime = GetEncounterStartTime(time);
            var nextEncounterTime = GetNextEncounterStartTime(encounterTime);
            if (currentEncounter.IsPvpEncounter && mostRecentClassChangeTime < encounterTime || (nextEncounterTime < mostRecentClassChangeTime && nextEncounterTime != encounterTime))
                return new SWTORClass();

            var classAtTime = classOfSource[mostRecentClassChangeTime];
            return classAtTime;
        }
        public SWTORClass GetCharacterClassAtTime(string entityString, DateTime time)
        {
            var entity = PlayerClassChangeInfo.Keys.FirstOrDefault(e => e.Name.ToLower() == entityString || e.LogId.ToString() == entityString);
            if (entity == null || !PlayerClassChangeInfo.TryGetValue(entity, out var classOfSource))
                return new SWTORClass();
            if (classOfSource == null)
                return new SWTORClass();

            var mostRecentClassChangeTime = classOfSource.Keys.ToList().MinBy(l => Math.Abs((time - l).TotalSeconds));
            var currentEncounter = GetEncounterActiveAtTime(time);
            var encounterTime = GetEncounterStartTime(time);
            var nextEncounterTime = GetNextEncounterStartTime(encounterTime);
            if (currentEncounter.IsPvpEncounter && mostRecentClassChangeTime < encounterTime || (nextEncounterTime < mostRecentClassChangeTime && nextEncounterTime != encounterTime))
                return new SWTORClass();

            var classAtTime = classOfSource[mostRecentClassChangeTime];
            return classAtTime;
        }

        private DateTime GetNextEncounterStartTime(DateTime currentEncounterStartTime)
        {
            if (_orderedEncounterChangeTimes.Count == 0 || !_orderedEncounterChangeTimes.Contains(currentEncounterStartTime))
            {
                return currentEncounterStartTime;
            }
            return _orderedEncounterChangeTimes.Last() == currentEncounterStartTime ? currentEncounterStartTime :
                _orderedEncounterChangeTimes[_orderedEncounterChangeTimes.IndexOf(currentEncounterStartTime) + 1];
        }

        public EntityInfo GetLocalPlayerTargetAtTime(DateTime time)
        {
            if (LocalPlayer == null)
                return new EntityInfo();
            return GetPlayerTargetAtTime(LocalPlayer, time);
        }
        public EntityInfo GetPlayerTargetAtTime(Entity? player, DateTime time)
        {
            if (player == null || !PlayerTargetsInfo.TryGetValue(player, out var targets))
                return new EntityInfo();
            var targetKeys = targets.Keys;
            return targetKeys.Any(v => v <= time) ? targets[targetKeys.Where(v => v <= time).MinBy(l => Math.Abs((time - l).TotalSeconds))] : new EntityInfo();
        }
        public EntityInfo GetEnemyTargetAtTime(Entity enemy, DateTime time)
        {
            if (!EnemyTargetsInfo.TryGetValue(enemy, out var targets))
                return new EntityInfo();
            var targetKeys = targets.Keys;
            return targetKeys.Any(v => v <= time) ? targets[targetKeys.Where(v => v <= time).MinBy(l => Math.Abs((time - l).TotalSeconds))] : new EntityInfo();
        }
        public List<CombatModifier> GetEffectsWithSource(DateTime startTime, DateTime endTime, Entity owner)
        {
            var allMods = Modifiers.SelectMany(kvp => kvp.Value);
            var inScopeModifiers = allMods.Where(m => !(m.Value.StartTime < startTime && m.Value.StopTime < startTime) && !(m.Value.StartTime > endTime && m.Value.StopTime > endTime) && m.Value.Source == owner).Select(kvp => kvp.Value);
            return GetEffects(startTime, endTime, inScopeModifiers);
        }
        public List<CombatModifier> GetEffectsWithTarget(DateTime startTime, DateTime endTime, Entity owner)
        {
            var allMods = Modifiers.SelectMany(kvp => kvp.Value);
            var inScopeModifiers = allMods.Where(m => !(m.Value.StartTime < startTime && m.Value.StopTime < startTime) && !(m.Value.StartTime > endTime && m.Value.StopTime > endTime) && m.Value.Target == owner).Select(kvp => kvp.Value);
            return GetEffects(startTime, endTime, inScopeModifiers);
        }
        public List<CombatModifier> GetEffectsWithTarget(DateTime timestamp, Entity owner)
        {
            var allMods = Modifiers.SelectMany(kvp => kvp.Value);
            var inScopeModifiers = allMods.Where(m => (m.Value.StartTime < timestamp && m.Value.StopTime > timestamp) && m.Value.Target == owner).Select(kvp => kvp.Value);
            return GetEffects(timestamp, inScopeModifiers);
        }
        public List<CombatModifier> GetPersonalEffects(DateTime startTime, DateTime endTime, Entity owner)
        {
            var allMods = Modifiers.SelectMany(kvp => kvp.Value);
            var inScopeModifiers = allMods.Where(m => !(m.Value.StartTime < startTime && m.Value.StopTime < startTime) && !(m.Value.StartTime > endTime && m.Value.StopTime > endTime) && m.Value.Source == owner && m.Value.Target == owner).Select(kvp => kvp.Value);
            return GetEffects(startTime, endTime, inScopeModifiers);
        }
        public List<CombatModifier> GetCurrentlyActiveRaidHOTS(DateTime time)
        {
            ulong koltoShellsId = 985226842996736;
            ulong traumaProbeId = 999516199190528;
            List<ulong> longRunningHotIds = new List<ulong>() { koltoShellsId, traumaProbeId };
            List<CombatModifier> activeHots = new List<CombatModifier>();
            foreach (var hotId in longRunningHotIds)
            {
                activeHots.AddRange(GetInstancesOfEffectAtTime(time, hotId));
            }
            return activeHots;
        }
        public List<CombatModifier> GetInstancesOfEffectAtTime(DateTime time, ulong effect)
        {
            if (!Modifiers.TryGetValue(effect, out var instancesOfEffect))
                return new List<CombatModifier>();
            var activeModifiersOnPlayer = instancesOfEffect.Where(m =>
                m.Value.StartTime <= time && (m.Value.StopTime > time || m.Value.StopTime == DateTime.MinValue)).Select(kvp => kvp.Value).ToList();
            return activeModifiersOnPlayer;
        }
        public List<CombatModifier> GetInstancesOfEffectOnEntityAtTime(DateTime time, string entity, ulong effect)
        {
            if (!Modifiers.TryGetValue(effect, out var instancesOfEffect))
                return new List<CombatModifier>();
            var activeModifiersOnPlayer = instancesOfEffect.Where(m =>
                m.Value.StartTime <= time && (m.Value.StopTime > time || m.Value.StopTime == DateTime.MinValue) &&
                m.Value.Target.Id.ToString() == entity || m.Value.Target.Name == entity).Select(kvp => kvp.Value).ToList();
            return activeModifiersOnPlayer;
        }
        private static List<CombatModifier> GetEffects(DateTime startTime, DateTime endTime, IEnumerable<CombatModifier> inScopeModifiers)
        {
            var result = new List<CombatModifier>();

            foreach (var m in inScopeModifiers)
            {
                var start = m.StartTime < startTime ? startTime : m.StartTime;
                var stop = (m.StopTime == DateTime.MinValue || m.StopTime > endTime) ? endTime : m.StopTime;

                if ((stop - start).TotalSeconds <= 0)
                    continue;

                if (m.StartTime < startTime || m.StopTime > endTime || m.StopTime == DateTime.MinValue)
                {
                    result.Add(new CombatModifier
                    {
                        EffectId = m.EffectId,
                        EffectName = m.EffectName,
                        Source = m.Source,
                        Target = m.Target,
                        Type = m.Type,
                        Name = m.Name,
                        StartTime = start,
                        StopTime = stop,
                        ChargesAtTime = m.ChargesAtTime
                    });
                }
                else
                {
                    result.Add(m);
                }
            }

            return result;

        }
        private static List<CombatModifier> GetEffects(DateTime timestamp, IEnumerable<CombatModifier> inScopeModifiers)
        {
            var correctedModifiers = inScopeModifiers.Select(m =>
            {
                CombatModifier correctedModifier = new CombatModifier();
                if (m.StopTime == DateTime.MinValue || m.StartTime < timestamp || m.StopTime > timestamp)
                {
                    correctedModifier.EffectId = m.EffectId;
                    correctedModifier.EffectName = m.EffectName;
                    correctedModifier.Source = m.Source;
                    correctedModifier.Target = m.Target;
                    correctedModifier.Type = m.Type;
                    correctedModifier.Name = m.Name;
                    correctedModifier.StartTime = m.StartTime;
                    correctedModifier.StopTime = m.StopTime;
                    correctedModifier.ChargesAtTime = m.ChargesAtTime;
                    if (m.StopTime == DateTime.MinValue)
                    {
                        correctedModifier.StopTime = timestamp;
                    }
                    if (m.StopTime > timestamp)
                    {
                        correctedModifier.StopTime = timestamp;
                    }
                    if (m.StartTime < timestamp)
                    {
                        correctedModifier.StartTime = timestamp;
                    }
                    return correctedModifier;
                }
                return m;
            });
            return correctedModifiers.ToList();

        }

        internal void CacheEncounterEnterList()
        {
            _orderedEncounterChangeTimes = EncounterEnteredInfo.Keys.OrderBy(t => t).ToList();
        }
    }
}
