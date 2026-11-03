using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayScalarToFontSize : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var defaultFontSize = (string)parameter == "Large" ? 20 : 18;
            var scalarValue = (double)value;
            return scalarValue * defaultFontSize;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
