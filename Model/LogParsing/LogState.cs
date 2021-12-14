﻿using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
        Debuff
    }
    public enum LogVersion
    {
        Legacy,
        NextGen
    }
    public class CombatModifier
    {
        public string Name { get; set; }
        public CombatModfierType Type { get; set; }
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public double DurationSeconds => StopTime == DateTime.MinValue? 0:(StopTime - StartTime).TotalSeconds;
    }
    public class LogState
    {
        private static List<string> _healingDisciplines = new List<string> { "Corruption", "Medicine", "Bodyguard", "Seer", "Sawbones", "Combat Medic" };
        private static List<string> _tankDisciplines = new List<string> { "Darkness", "Immortal", "Sheild Tech", "Kinentic Combat", "Defense", "Sheild Specialist" };
        public ConcurrentDictionary<Entity,Dictionary<DateTime, SWTORClass>> PlayerClassChangeInfo = new ConcurrentDictionary<Entity, Dictionary<DateTime, SWTORClass>>();
        public Dictionary<Entity, Dictionary<DateTime, bool>> PlayerDeathChangeInfo = new Dictionary<Entity, Dictionary<DateTime, bool>>();
        public Dictionary<DateTime, EncounterInfo> EncounterEnteredInfo = new Dictionary<DateTime, EncounterInfo>();
        public LogVersion LogVersion { get; set; } = LogVersion.Legacy;
        public List<ParsedLogEntry> RawLogs { get; set; } = new List<ParsedLogEntry>();
        public string CurrentLocation { get; set; }
        public long MostRecentLogIndex = 0;
        public List<CombatModifier> Modifiers { get; set; } = new List<CombatModifier>();
        public object modifierLogLock = new object();
        public Dictionary<Entity, PositionData> CurrentCharacterPositions { get; set; } = new Dictionary<Entity, PositionData>();
        public bool WasPlayerDeadAtTime(Entity player, DateTime timestamp)
        {
            var playerDeathInfo = PlayerDeathChangeInfo[player];
            if (!playerDeathInfo.Any(d => d.Key < timestamp))
                return true;
            var updateTimes = playerDeathInfo.Keys.ToList();
            for(var i=0;i<updateTimes.Count;i++)
            {
                if (updateTimes[i] > timestamp)
                    return playerDeathInfo[updateTimes[i-1]];
            }
            return false;
        }
        public EncounterInfo GetEncounterActiveAtTime(DateTime time)
        {
            var encounterChangeTimes = EncounterEnteredInfo.Keys.OrderBy(t => t).ToList();
            if (encounterChangeTimes.Count == 0 || time < encounterChangeTimes.First())
                return new EncounterInfo() { Name = "Unknown Encounter" };
            if (time > encounterChangeTimes.Last())
                return EncounterEnteredInfo[encounterChangeTimes.Last()];
            for(var i = 0; i < encounterChangeTimes.Count; i++)
            {
                if (encounterChangeTimes[i] == time)
                    return EncounterEnteredInfo[encounterChangeTimes[i]];
                if (encounterChangeTimes[i] > time)
                    return EncounterEnteredInfo[encounterChangeTimes[i - 1]];
            }
            return new EncounterInfo() { Name = "Unknown Encounter" };
        }
        public double GetCurrentHealsPerThreat(DateTime timeStamp, Entity source)
        {
            var classOfSource = CharacterClassHelper.GetClassFromEntityAtTime(source, timeStamp);
            double healsPerThreat = 2;
            double healsModifier = 1;
            if (classOfSource == null)
                return healsPerThreat;
            if (_healingDisciplines.Contains(classOfSource.Discipline))
                healsModifier -= 0.1d;
            if (_tankDisciplines.Contains(classOfSource.Discipline))
                healsModifier += 1.5d;
            //if (GetCombatModifiersAtTime(timeStamp).Any(m => m.Type == CombatModfierType.GuardedThreatReduced))
            //    healsModifier -= .25d; //healsPerThreat *= 1.25;
            return healsPerThreat/healsModifier;
        }
        //public List<CombatModifier> GetCombatModifiersAtTime(DateTime timeStamp)
        //{
        //    lock (modifierLogLock)
        //    {
        //        List<CombatModifier> mods = new List<CombatModifier>();
        //        foreach (var mod in Modifiers)
        //        {
        //            if (mod.StartTime > timeStamp)
        //                continue;
        //            if (mod.StopTime < timeStamp && mod.StartTime != DateTime.MinValue)
        //                continue;
        //            mods.Add(mod);
        //        }

        //        return mods;
        //    }
        //    //return Modifiers.Where(m => m.StartTime < timeStamp && (m.StopTime >= timeStamp || m.StopTime == DateTime.MinValue)).ToList();
        //}
        //public List<CombatModifier> GetCombatModifiersAtTimeInvolvingParticipants(DateTime timeStamp,Entity source, Entity target)
        //{
        //    return GetCombatModifiersAtTime(timeStamp).Where(m => m.Source == source || m.Source == target || m.Target == source || m.Target == target).ToList();
        //}
        public List<CombatModifier> GetEffectsWithSource(DateTime startTime, DateTime endTime, Entity owner)
        {
            var inScopeModifiers = Modifiers.Where(m => !(m.StartTime < startTime && m.StopTime < startTime) && !(m.StartTime > endTime && m.StopTime > endTime) && m.Source == owner).ToList();
            return GetEffects(startTime, endTime, inScopeModifiers);
        }
        public List<CombatModifier> GetEffectsWithTarget(DateTime startTime, DateTime endTime, Entity owner)
        {
            var inScopeModifiers = Modifiers.Where(m => !(m.StartTime < startTime && m.StopTime < startTime) && !(m.StartTime > endTime && m.StopTime > endTime) && m.Target == owner).ToList();
            return GetEffects(startTime, endTime, inScopeModifiers);
        }
        public List<CombatModifier> GetPersonalEffects(DateTime startTime, DateTime endTime, Entity owner)
        {
            var inScopeModifiers = Modifiers.Where(m => !(m.StartTime < startTime && m.StopTime < startTime) && !(m.StartTime > endTime && m.StopTime > endTime) && m.Source == owner && m.Target == owner).ToList();
            return GetEffects(startTime, endTime, inScopeModifiers);
        }

        private static List<CombatModifier> GetEffects(DateTime startTime, DateTime endTime, List<CombatModifier> inScopeModifiers)
        {
            var correctedModifiers = inScopeModifiers.Select(m =>
            {
                CombatModifier correctedModifier = new CombatModifier();
                if (m.StopTime == DateTime.MinValue || m.StartTime < startTime || m.StopTime > endTime)
                {
                    correctedModifier.Source = m.Source;
                    correctedModifier.Type = m.Type;
                    correctedModifier.Name = m.Name;
                    correctedModifier.StartTime = m.StartTime;
                    correctedModifier.StopTime = m.StopTime;
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
    }
}
