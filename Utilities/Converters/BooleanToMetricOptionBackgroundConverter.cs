using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    internal class BooleanToMetricOptionBackgroundConverter : IValueConverter
    {
        object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            bool isInverted = parameter != null && ((string)parameter).ToLower() == "inverted";

            if (value is bool booleanValue)
            {
                if (isInverted)
                {
                    return !booleanValue
                        ? new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray11"))
                        : new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray4"));
                }

                return booleanValue
                    ? Brushes.WhiteSmoke
                    : new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray4"));
            }

            return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray4"));
        }

        object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
