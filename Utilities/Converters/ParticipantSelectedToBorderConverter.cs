using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class ParticipantSelectedToBorderConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected
                    ? new SolidColorBrush(ResourceFinder.GetColorFromResourceName("DarkGrayGreenColor"))
                    : new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray1"));
            }

            return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray1"));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
