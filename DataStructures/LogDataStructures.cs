using System;
using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.DataStructures
{
    public class DisplayableLogEntry
    {
        public DisplayableLogEntry(string sec, string source, string target, string ability, string effectName, string value, bool wasValueCrit, string type, string modifiertype, string modifierValue)
        {
            SecondsSinceCombatStart = sec;
            Source = source;
            Target = target;
            Ability = ability;
            EffectName = effectName;
            Value = value;
            WasValueCrit = wasValueCrit;
            ValueType = type;
            ModifierType = modifiertype;
            ModifierValue = modifierValue;
        }
        public string SecondsSinceCombatStart { get; }
        public string Source { get; }
        public string Target { get; }
        public string Ability { get; }
        public string EffectName { get; }
        public string Value { get; }
        public bool WasValueCrit { get; }
        public string ValueType { get; }
        public string ModifierType { get; }
        public string ModifierValue { get; }
    }
    public class ParsedLogEntry
    {
        public ErrorType Error;
        public long RaidLogId;
        public string LogName;
        public string LogText;
        public long LogLineNumber { get; set; }
        public string LogLocation { get; set; }
        public string LogLocationId { get; set; }
        public string LogDifficultyId { get; set; }
        public DateTime TimeStamp { get; set; }
        public double SecondsSinceCombatStart { get; set; }
        public Entity Source => SourceInfo.Entity;
        public EntityInfo SourceInfo { get; set; }
        public Entity Target => TargetInfo.Entity;
        public EntityInfo TargetInfo { get; set; }
        public string Ability { get; set; }
        public string AbilityId { get; set; }
        public Effect Effect { get; set; }
        public Value Value { get; set; }
        public long Threat { get; set; }
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
        public static Entity EmptyEntity = new Entity();
        public string Name { get; set; }
        public long Id { get; set; }
        public long LogId { get; set; }
        public bool IsCharacter;
        public bool IsLocalPlayer;
        public bool IsCompanion;
        public bool IsBoss;
    }
    public class EntityInfo
    {
        public SWTORClass Class { get; set; }
        public Entity Entity { get; set; } = new Entity();
        public PositionData Position { get; set; } = new PositionData();
        public double MaxHP { get; set; }
        public double CurrentHP { get; set; } = -500;
        public bool IsAlive { get; set; }
    }
    public class Effect
    {
        public EffectType EffectType { get; set; }
        public string EffectName { get; set; }
        public string EffectId { get; set; }
        public string SecondEffectId { get; set; }
    }
    public class Value
    {
        public double DblValue;
        public double EffectiveDblValue;
        public string StrValue;
        public string DisplayValue { get; set; }
        public string ModifierDisplayValue { get; set; }
        public string ModifierType { get; set; } 
        public string AllBuffs => string.Join(',',Buffs.Select(b=>b.Name));
        public List<CombatModifier> Buffs { get; set; } = new List<CombatModifier>();
        public List<CombatModifier> DefensiveBuffs { get; set; } = new List<CombatModifier>();
        public string AllDefensiveBuffs => string.Join(',', DefensiveBuffs.Select(db => db.Name));
        
        public DamageType ValueType { get; set; }
        public string  ValueTypeId { get; set; }

        public Value Modifier;
        public bool WasCrit { get; set; }
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
        TargetChanged,
        ModifyCharges,
        AbsorbShield
    }
    public enum DamageType
    {
        none,
        heal,
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
