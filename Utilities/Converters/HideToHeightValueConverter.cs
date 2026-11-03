using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    public class HideToHeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSelected = (bool)value;
            if (isSelected)
                return GridLength.Auto;
            else
                return new GridLength(0.5, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
