using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    class EffectSelectedToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var selected = (bool)value;
            if (selected)
                return "✓";
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
