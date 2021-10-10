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
        public Entity Source => SourceInfo.Entity;
        public EntityInfo SourceInfo { get; set; }
        public Entity Target => TargetInfo.Entity;
        public EntityInfo TargetInfo { get; set; }
        public string Ability { get; set; }
        public Effect Effect { get; set; }
        public Value Value { get; set; }
        public int Threat { get; set; }

    }
    public class PositionData
    {
        public double X;
        public double Y;
        public double Facing;
        public double Z;
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
    public class EntityInfo
    {
        public Entity Entity { get; set; } = new Entity();
        public PositionData Position { get; set; } = new PositionData();
        public double MaxHP { get; set; }
        public double CurrentHP { get; set; }
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
        AreaEntered,
        DisciplineChanged,
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
