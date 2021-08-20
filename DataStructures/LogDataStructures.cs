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

    }
    public enum ErrorType
    {
        None,
        IncompleteLine
    }
    public class Entity
    {
        public string Name;
        public bool IsCharacter;
        public bool IsPlayer;
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
