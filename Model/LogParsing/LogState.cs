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
        public string EffectId { get; set; }
        public CombatModfierType Type { get; set; }
        public Dictionary<DateTime, int> ChargesAtTime { get; set; } = new Dictionary<DateTime, int>();
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public bool Complete { get; set; }
        public double DurationSeconds => StopTime == DateTime.MinValue ? 0 : (StopTime - StartTime).TotalSeconds;
    }
    public class LogState
    {
        private List<DateTime> _orderedEncounterChangeTimes = new List<DateTime>();

        public ConcurrentDictionary<Entity, Dictionary<DateTime, SWTORClass>> PlayerClassChangeInfo = new ConcurrentDictionary<Entity, Dictionary<DateTime, SWTORClass>>();
        public ConcurrentDictionary<Entity, Dictionary<DateTime, EntityInfo>> PlayerTargetsInfo = new ConcurrentDictionary<Entity, Dictionary<DateTime, EntityInfo>>();
        public Dictionary<Entity, Dictionary<DateTime, bool>> PlayerDeathChangeInfo = new Dictionary<Entity, Dictionary<DateTime, bool>>();
        public Dictionary<DateTime, EncounterInfo> EncounterEnteredInfo = new Dictionary<DateTime, EncounterInfo>();

        public LogVersion LogVersion { get; set; } = LogVersion.Legacy;
        public List<ParsedLogEntry> RawLogs { get; set; } = new List<ParsedLogEntry>();
        public string CurrentLocation { get; set; }
        public ConcurrentDictionary<string, ConcurrentDictionary<Guid, CombatModifier>> Modifiers { get; set; } = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, CombatModifier>>();
        public Dictionary<Entity, PositionData> CurrentCharacterPositions { get; set; } = new Dictionary<Entity, PositionData>();
        public PositionData CurrentLocalCharacterPosition => LocalPlayer == null ? new PositionData() : CurrentCharacterPositions[LocalPlayer];
        public Entity LocalPlayer { get; internal set; }

        private static object _effectsModLock = new object();


        public bool WasPlayerDeadAtTime(Entity player, DateTime timestamp)
        {
            if (!PlayerDeathChangeInfo.ContainsKey(player))
            {
                return true;
            }
            var playerDeathInfo = PlayerDeathChangeInfo[player];
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
                if (updateTimes[i] > timestamp)
                    return playerDeathInfo[updateTimes[i - 1]];
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
            if (entity == null || !PlayerClassChangeInfo.ContainsKey(entity))
                return new SWTORClass();
            var classOfSource = PlayerClassChangeInfo[entity];
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
            if (entity == null || !PlayerClassChangeInfo.ContainsKey(entity))
                return new SWTORClass();
            var classOfSource = PlayerClassChangeInfo[entity];
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
            if (!_orderedEncounterChangeTimes.Any() || !_orderedEncounterChangeTimes.Contains(currentEncounterStartTime))
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
        public EntityInfo GetPlayerTargetAtTime(Entity player, DateTime time)
        {
            if (!PlayerTargetsInfo.ContainsKey(player))
                return new EntityInfo();
            var targets = PlayerTargetsInfo[player];
            var targetKeys = targets.Keys.ToList();
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
        public List<CombatModifier> GetPersonalEffects(DateTime startTime, DateTime endTime, Entity owner)
        {
                var allMods = Modifiers.SelectMany(kvp => kvp.Value);
                var inScopeModifiers = allMods.Where(m => !(m.Value.StartTime < startTime && m.Value.StopTime < startTime) && !(m.Value.StartTime > endTime && m.Value.StopTime > endTime) && m.Value.Source == owner && m.Value.Target == owner).Select(kvp => kvp.Value);
                return GetEffects(startTime, endTime, inScopeModifiers);
        }
        public List<CombatModifier> GetCurrentlyActiveRaidHOTS(DateTime time)
        {
            string koltoShellsId = "985226842996736";
            string traumaProbeId = "999516199190528";
            List<string> longRunningHotIds = new List<string>() { koltoShellsId, traumaProbeId };
            List<CombatModifier> activeHots = new List<CombatModifier>();
            foreach (var hotId in longRunningHotIds)
            {
                activeHots.AddRange(GetInstancesOfEffectAtTime(time, hotId));
            }
            return activeHots;
        }
        public List<CombatModifier> GetInstancesOfEffectAtTime(DateTime time, string effect)
        {
            if (!Modifiers.ContainsKey(effect))
                return new List<CombatModifier>();
            var instancesOfEffect = Modifiers[effect];
            var activeModifiersOnPlayer = instancesOfEffect.Where(m =>
                m.Value.StartTime <= time && (m.Value.StopTime > time || m.Value.StopTime == DateTime.MinValue)).Select(kvp => kvp.Value).ToList();
            return activeModifiersOnPlayer;
        }
        public List<CombatModifier> GetInstancesOfEffectOnEntityAtTime(DateTime time, string entity, string effect)
        {
            if (!Modifiers.ContainsKey(effect))
                return new List<CombatModifier>();
            var instancesOfEffect = Modifiers[effect];
            var activeModifiersOnPlayer = instancesOfEffect.Where(m =>
                m.Value.StartTime <= time && (m.Value.StopTime > time || m.Value.StopTime == DateTime.MinValue) &&
                m.Value.Target.Id.ToString() == entity || m.Value.Target.Name == entity).Select(kvp => kvp.Value).ToList();
            return activeModifiersOnPlayer;
        }

        private static List<CombatModifier> GetEffects(DateTime startTime, DateTime endTime, IEnumerable<CombatModifier> inScopeModifiers)
        {
                var correctedModifiers = inScopeModifiers.Select(m =>
                {
                    CombatModifier correctedModifier = new CombatModifier();
                    if (m.StopTime == DateTime.MinValue || m.StartTime < startTime || m.StopTime > endTime)
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
                            correctedModifier.StopTime = endTime;
                        }
                        if (m.StopTime > endTime)
                        {
                            correctedModifier.StopTime = endTime;
                        }
                        if (m.StartTime < startTime)
                        {
                            correctedModifier.StartTime = startTime;
                        }
                        return correctedModifier;
                    }
                    return m;
                });
                return correctedModifiers.Where(m => m.DurationSeconds > 0).ToList();
        }

        internal void CacheEncounterEnterList()
        {
            _orderedEncounterChangeTimes = EncounterEnteredInfo.Keys.OrderBy(t => t).ToList();
        }
    }
}
