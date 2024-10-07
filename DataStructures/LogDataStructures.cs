using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace SWTORCombatParser.DataStructures
{
    public class DisplayableLogEntry:ReactiveObject
    {
        private string _sourceId;
        private string _targetId;
        private string _abilityId;
        private string _effectId;
        private readonly static SolidColorBrush _deathBackground = new(Brushes.IndianRed.Color);
        private readonly static SolidColorBrush _deathBackgroundWithSource = new(Brushes.Crimson.Color);
        private readonly static SolidColorBrush _revivedBackground = new(Brushes.CornflowerBlue.Color);
        private readonly static SolidColorBrush _damageBackground = new (Color.Parse("#613b3b"));
        public DisplayableLogEntry(string sec, string source, string sourceId, string target, string targetId, string ability, string abilityId, string effectName, string effectId, string value, bool wasValueCrit, string type, string modifiertype, string modifierValue, double maxValue, double logValue, double threat)
        {
            _sourceId = sourceId;
            _targetId = targetId;
            _abilityId = abilityId;
            _effectId = effectId;

            SecondsSinceCombatStart = sec;
            Source = source;
            Target = target;
            Ability = ability;

            EffectName = effectName;

            EffectBackground = new SolidColorBrush(Brushes.Transparent.Color);
            ValueBackground = new SolidColorBrush(Brushes.Transparent.Color);

            AbilityTextMargin = !string.IsNullOrEmpty(_abilityId) && IconGetter.HasIcon(_abilityId) ? new Thickness(18, 0, 0, 0) : new Thickness(5, 0, 0, 0);
            EffectTextMargin = !string.IsNullOrEmpty(_effectId) && IconGetter.HasIcon(_effectId) ? new Thickness(18, 0, 0, 0) : new Thickness(5, 0, 0, 0);
            if (effectId == _7_0LogParsing.DeathCombatId)
            {
                EffectBackground = _deathBackground;
            }
            if (effectId == _7_0LogParsing.DeathCombatId && !string.IsNullOrEmpty(source))
            {
                EffectBackground = _deathBackgroundWithSource;
            }
            if (effectId == _7_0LogParsing.RevivedCombatId)
            {
                EffectBackground = _revivedBackground;
            }
            if (effectId == _7_0LogParsing._damageEffectId)
            {
                EffectBackground = _damageBackground;

            }
            if (double.TryParse(value, out var r))
            {
                ValueBackground = new SolidColorBrush(GetColorForValue(logValue / maxValue));
            }
            Value = value;
            Threat = threat.ToString();
            WasValueCrit = wasValueCrit;
            ValueType = type;
            ModifierType = modifiertype;
            ModifierValue = modifierValue;
        }
        public async Task AddIcons()
        {
            AbilityIcon = !string.IsNullOrEmpty(_abilityId) && IconGetter.HasIcon(_abilityId) ? await IconGetter.GetIconForId(_abilityId) : null;
            EffectIcon = !string.IsNullOrEmpty(_effectId) && IconGetter.HasIcon(_effectId) ? await IconGetter.GetIconForId(_effectId) : null;
        }
        public SolidColorBrush EffectBackground { get; set; }
        public SolidColorBrush ValueBackground { get; set; }
        public string SecondsSinceCombatStart { get; }
        public string Source { get; }
        public string Target { get; }
        public string Ability { get; }
        public Bitmap AbilityIcon { get; set; }
        public string EffectName { get; }
        public Bitmap EffectIcon { get; set; }
        public Thickness EffectTextMargin { get; set; }
        public Thickness AbilityTextMargin { get; set; }
        public string Value { get; }
        public string Threat { get; set; }
        public bool WasValueCrit { get; }
        public string ValueType { get; }
        public string ModifierType { get; }
        public string ModifierValue { get; }
        public ReactiveCommand<string, Unit> CellClickedCommand => ReactiveCommand.Create<string>(CellClicked);

        private void CellClicked(string obj)
        {
            //use the cross platform clipboard class to copy the data to the clipboard
            switch (obj)
            {
                case "Source":
                    CrossPlatformClipboard.SetText(_sourceId);
                    break;
                case "Target":
                    CrossPlatformClipboard.SetText(_targetId);
                    break;
                case "Ability":
                    CrossPlatformClipboard.SetText(_abilityId);
                    break;
                case "Effect":
                    CrossPlatformClipboard.SetText(_effectId);
                    break;

            }
        }
        private Color GetColorForValue(double fraction)
        {
            Color startColor = Colors.Transparent;
            Color endColor = Colors.Red;


            // Calculate the color based on the linear interpolation formula
            byte a = (byte)(startColor.A + (endColor.A - startColor.A) * fraction);
            byte r = (byte)(startColor.R + (endColor.R - startColor.R) * fraction);
            byte g = (byte)(startColor.G + (endColor.G - startColor.G) * fraction);
            byte b = (byte)(startColor.B + (endColor.B - startColor.B) * fraction);

            // Create a new color object
            Color selectedColor = Color.FromArgb(a, r, g, b);
            return selectedColor;
        }
    }
    public class ParsedLogEntry
    {
        public ErrorType Error;
        public long RaidLogId;
        public string LogName;
        public int LogBytes;
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
        public string ModifierEffectName => string.Intern(Ability + AddSecondHalf(Ability, Effect.EffectName));
        public Value Value { get; set; }
        public double Threat { get; set; }

        private static string AddSecondHalf(string firstHalf, string effectName)
        {
            if (firstHalf == effectName)
                return "";
            else
                return ": " + effectName;
        }

        public List<string> Strings() {
            return [
                Effect.EffectName,
                Effect.EffectId,
                Ability,
                AbilityId,
                Source.Name,
                Source.LogId.ToString(),
                Target.Name,
                Target.LogId.ToString(),
            ];
        }
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
        public Entity Entity { get; set; } = Entity.EmptyEntity;
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
        public double MitigatedDblValue;
        public string StrValue;
        public string DisplayValue { get; set; }
        public string ModifierDisplayValue { get; set; }
        public string ModifierType { get; set; }
        public string AllBuffs => string.Join(',', Buffs.Select(b => b.Name));
        public List<CombatModifier> Buffs { get; set; } = new List<CombatModifier>();
        public List<CombatModifier> DefensiveBuffs { get; set; } = new List<CombatModifier>();
        public string AllDefensiveBuffs => string.Join(',', DefensiveBuffs.Select(db => db.Name));

        public DamageType ValueType { get; set; }
        public string ValueTypeId { get; set; }

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
        AbsorbShield,
        Unknown,
        ModifyThreat
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
