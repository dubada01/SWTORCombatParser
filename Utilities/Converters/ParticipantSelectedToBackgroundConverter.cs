using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class ParticipantSelectedToBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected
                    ? new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray4"))
                    : new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray2"));
            }

            return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray2"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
