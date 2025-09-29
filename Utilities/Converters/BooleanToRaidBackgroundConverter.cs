using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;


namespace SWTORCombatParser.Utilities.Converters
{
    public class BooleanToRaidBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolVal)
            {
                return boolVal
                    ? new SolidColorBrush((Color)ResourceFinder.GetColorFromResourceName("Gray2"))
                    : Brushes.Transparent;
            }
            return Brushes.Transparent;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
