using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayScalarToFontSize : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var defaultFontSize = parameter?.ToString() == "Large" ? 20 : 18;
            if (value is double scalarValue)
            {
                return scalarValue * defaultFontSize;
            }

            return defaultFontSize;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
