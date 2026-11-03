using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;


namespace SWTORCombatParser.Utilities.Converters
{
    public class ValueToPixelGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new GridLength((double)value * 35, GridUnitType.Pixel);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
