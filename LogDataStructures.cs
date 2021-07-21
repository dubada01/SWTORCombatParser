using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser
{
    public class ParsedLogEntry
    {
        public ErrorType Error;

        public string LogName;
        public DateTime TimeStamp;
        public Entity Source;
        public Entity Target;
        public string Ability;
        public Effect Effect;
        public Value Value;
        public int Threat;

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
        public string StrValue;

        public ValueType Type;
        public DamageType DamageType;

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
        unknown
    }
    public enum ValueType
    {
        Damage,
        Location,
        Resource
    }
}
