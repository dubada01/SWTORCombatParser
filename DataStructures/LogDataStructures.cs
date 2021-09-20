using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser
{
    public class ParsedLogEntry
    {
        public ErrorType Error;
        public long RaidLogId;
        public string LogName;
        public string LogText;
        public long LogLineNumber { get; set; }
        public PositionData Position { get; set; }
        public DateTime TimeStamp { get; set; }
        public Entity Source { get; set; }
        public Entity Target { get; set; }
        public string Ability { get; set; }
        public Effect Effect { get; set; }
        public Value Value { get; set; }
        public int Threat { get; set; }
        public static ParsedLogEntry GetDummyLog(string message, DateTime logTime = new DateTime(), long raidId = 0)
        {
            var dummy = new ParsedLogEntry
            {
                RaidLogId = raidId,
                TimeStamp =DateTime.MinValue == logTime?DateTime.Now:logTime,
                Source = new Entity(),
                Target = new Entity(),
                Ability = message,
                Effect = new Effect(),
                Value = new Value
                {
                    Modifier = new Value()
                }
            };
            return dummy;
        }
        public static ParsedLogEntry GetEndCombatLog(DateTime timestamp, string logPath)
        {
            return new ParsedLogEntry
            {
                TimeStamp = timestamp,
                Ability = "SWTOR_PARSING_COMBAT_END",
                Source = new Entity(),
                Target = new Entity(),
                Value = new Value(),
                Effect = new Effect(),
                LogName = logPath,
            };
        }
        public ParsedLogEntry GetCompanionLog()
        {
            return new ParsedLogEntry
            {
                LogName = "companion_" + LogName,
                LogText = LogText,
                LogLineNumber = LogLineNumber,
                TimeStamp = TimeStamp,
                Source = Source,
                Target = Target,
                Ability = Ability,
                Effect = Effect,
                Value = Value,
                Threat = Threat
            };
        }
    }
    public class PositionData
    {
        public double X;
        public double Y;
        public double Facing;
    }
    public enum ErrorType
    {
        None,
        IncompleteLine
    }
    public class Entity
    {
        public string Name { get; set; }
        public bool IsCharacter;
        public bool IsLocalPlayer;
        public bool IsCompanion;
    }
    public class Effect
    {
        public EffectType EffectType;
        public string EffectName;
    }
    public class Value
    {
        public double DblValue;
        public double EffectiveDblValue;
        public string StrValue;

        public DamageType ValueType;

        public Value Modifier;
        public bool WasCrit;
    }
    public enum EffectType
    {
        Apply,
        Remove,
        Event,
        Spend,
        Restore,
    }
    public enum DamageType
    {
        kinetic,
        energy,
        intern,
        elemental,
        shield,
        miss,
        parry,
        deflect,
        dodge,
        immune,
        resist,
        cover,
        unknown,
        absorbed
    }
    public enum ValueType
    {
        Damage,
        Location,
        Resource
    }
}
