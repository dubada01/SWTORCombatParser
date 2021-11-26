using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public string LogLocation { get; set; }
        public DateTime TimeStamp { get; set; }
        public double SecondsSinceCombatStart { get; set; }
        public Entity Source => SourceInfo?.Entity;
        public EntityInfo SourceInfo { get; set; }
        public Entity Target => TargetInfo?.Entity;
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
        public long Id { get; set; }
        public bool IsCharacter;
        public bool IsLocalPlayer;
        public bool IsCompanion;
    }
    public class EntityInfo
    {
        public SWTORClass Class { get; set; }
        public Entity Entity { get; set; } = new Entity();
        public PositionData Position { get; set; } = new PositionData();
        public double MaxHP { get; set; }
        public double CurrentHP { get; set; }
    }
    public class Effect
    {
        public EffectType EffectType { get; set; }
        public string EffectName { get; set; }
    }
    public class Value
    {
        public double DblValue;
        public double EffectiveDblValue;
        public string StrValue;
        public string DisplayValue => string.IsNullOrEmpty(StrValue) ? ValueType == DamageType.none?"N/A": EffectiveDblValue.ToString("#,##0") : StrValue;
        public string ModifierDisplayValue => Modifier==null ? "0" : Modifier.EffectiveDblValue.ToString("#,##0");
        public string ModifierType => Modifier == null ? "N/A" : Modifier.ValueType.ToString();
        public string AllBuffs => string.Join(',',Buffs.Select(b=>b.Name));
        public List<CombatModifier> Buffs { get; set; } = new List<CombatModifier>();
        public List<CombatModifier> DefensiveBuffs { get; set; } = new List<CombatModifier>();
        public string AllDefensiveBuffs => string.Join(',', DefensiveBuffs.Select(db => db.Name));
        
        public DamageType ValueType { get; set; }

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
        ModifyCharges
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
