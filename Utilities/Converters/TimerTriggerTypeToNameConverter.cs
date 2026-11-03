using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;


namespace SWTORCombatParser.Utilities.Converters
{
    public class TimerTriggerTypeToNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var types = (List<TimerKeyType>)value;
            List<string> orderedConvertedNames = new List<string>();
            foreach (TimerKeyType type in types)
            {
                orderedConvertedNames.Add(GetConvertedType(type));
            }
            return orderedConvertedNames.OrderBy(v => v).ToList();
        }
        private string GetConvertedType(TimerKeyType type)
        {
            switch (type)
            {
                case TimerKeyType.CombatStart:
                    return "Combat Started";
                case TimerKeyType.EntityHP:
                    return "Entity HP";
                case TimerKeyType.AbilityUsed:
                    return "Ability Used";
                case TimerKeyType.FightDuration:
                    return "Combat Duration";
                case TimerKeyType.EffectGained:
                    return "Effect Gained";
                case TimerKeyType.EffectLost:
                    return "Effect Lost";
                case TimerKeyType.TimerExpired:
                    return "Timer Expired";
                case TimerKeyType.TargetChanged:
                    return "Target Changed";
                case TimerKeyType.DamageTaken:
                    return "Damage Taken";
                case TimerKeyType.HasEffect:
                    return "Has Effect";
                case TimerKeyType.IsFacing:
                    return "Is Facing";
                case TimerKeyType.And:
                    return "And";
                case TimerKeyType.Or:
                    return "Or";
                case TimerKeyType.IsTimerTriggered:
                    return "Is Timer Triggered?";
                case TimerKeyType.NewEntitySpawn:
                    return "Entity Spawned";
                case TimerKeyType.AbsorbShield:
                    return "Absorb Shield";
                case TimerKeyType.EntityDeath:
                    return "Entity Death";
                case TimerKeyType.VariableCheck:
                    return "Custom Variable";
                default:
                    return "Unknown";
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
