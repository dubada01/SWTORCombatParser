using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
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
        public Dictionary<Entity, SWTORClass> PlayerClasses = new Dictionary<Entity, SWTORClass>();
        public List<ParsedLogEntry> RawLogs { get; set; } = new List<ParsedLogEntry>();
        public string CurrentLocation { get; set; }
        public long MostRecentLogIndex = 0;
        public List<CombatModifier> Modifiers { get; set; } = new List<CombatModifier>();
        public Dictionary<Entity, PositionData> CurrentCharacterPositions { get; set; } = new Dictionary<Entity, PositionData>();
        public double GetCurrentHealsPerThreat(DateTime timeStamp, Entity source)
        {
            var classOfSource = PlayerClasses[source];
            double healsPerThreat = 2;
            double healsModifier = 1;
            if (classOfSource == null)
                return healsPerThreat;
            if (_healingDisciplines.Contains(classOfSource.Discipline))
                healsModifier -= 0.1d;
            if (_tankDisciplines.Contains(classOfSource.Discipline))
                healsModifier += 1.5d;
            if (GetCombatModifiersAtTime(timeStamp).Any(m => m.Type == CombatModfierType.GuardedThreatReduced))
                healsModifier -= .25d; //healsPerThreat *= 1.25;
            return healsPerThreat/healsModifier;
        }
        public List<CombatModifier> GetCombatModifiersAtTime(DateTime timeStamp)
        {
            return Modifiers.Where(m => m.StartTime < timeStamp && (m.StopTime >= timeStamp || m.StopTime == DateTime.MinValue)).ToList();
        }
        public List<CombatModifier> GetCombatModifiersAtTimeInvolvingParticipants(DateTime timeStamp,Entity source, Entity target)
        {
            return GetCombatModifiersAtTime(timeStamp).Where(m => m.Source == source || m.Source == target || m.Target == source || m.Target == target).ToList();
        }
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
