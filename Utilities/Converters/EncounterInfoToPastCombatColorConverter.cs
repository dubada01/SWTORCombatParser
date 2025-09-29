using SWTORCombatParser.DataStructures.EncounterInfo;
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class EncounterInfoToPastCombatColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is ValueTuple<EncounterInfo?, bool, SolidColorBrush> tupleValue)
            {
                var (encounterInfo, isActive, fallbackBrush) = tupleValue;

                if (encounterInfo == null)
                {
                    return isActive ? Brushes.MediumAquamarine : fallbackBrush;
                }

                return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("LightGrayGreenColor"));
            }

            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
