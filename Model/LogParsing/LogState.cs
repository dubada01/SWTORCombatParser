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
        Guarded,
        Guarding
    }
    public class CombatModifier
    {
        public string Name { get; set; }
        public CombatModfierType Type { get; set; }
        public string Source { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime StopTime { get; set; }
        public double DurationSeconds => StopTime == DateTime.MinValue? 0:(StopTime - StartTime).TotalSeconds;
    }
    public class LogState
    {
        private static List<string> _healingDisciplines = new List<string> { "Corruption", "Medicine", "Bodyguard", "Seer", "Sawbones", "Combat Medic" };
        private static List<string> _tankDisciplines = new List<string> { "Darkness", "Immortal", "Sheild Tech", "Kinentic Combat", "Defense", "Sheild Specialist" };
        public string PlayerName { get; set; }
        public SWTORClass PlayerClass { get; set; }
        public List<CombatModifier> Modifiers { get; set; } = new List<CombatModifier>();
        public double GetCurrentHealsPerThreat(DateTime timeStamp)
        {
            double healsPerThreat = 2;
            if (_healingDisciplines.Contains(PlayerClass.Discipline))
                healsPerThreat /= 0.9d;
            if (_tankDisciplines.Contains(PlayerClass.Discipline))
                healsPerThreat *= 0.4;
            if (GetCombatModifiersAtTime(timeStamp).Any(m => m.Type == CombatModfierType.Guarded))
                healsPerThreat *= 1.25;
            return healsPerThreat;
        }
        public List<CombatModifier> GetCombatModifiersAtTime(DateTime timeStamp)
        {
            return Modifiers.Where(m => m.StartTime < timeStamp && m.StopTime >= timeStamp).ToList();
        }
        public List<CombatModifier> GetCombatModifiersBetweenTimes(DateTime startTime, DateTime endTime)
        {
            return Modifiers.Where(m => m.StartTime > startTime && m.StartTime <= endTime).ToList();
        }
        public void ResetAll()
        {
            PlayerName = "";
            PlayerClass = null;
            Modifiers = new List<CombatModifier>();
        }
        public void ResetCombat()
        {
            PlayerClass = null;
            Modifiers = new List<CombatModifier>();
        }
    }
}
