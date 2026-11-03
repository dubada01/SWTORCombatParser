using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    class OverlayMoveableToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

