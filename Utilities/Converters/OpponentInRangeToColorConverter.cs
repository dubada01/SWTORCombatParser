using System;
using System.Globalization;
using Avalonia.Data.Converters;


namespace SWTORCombatParser.Utilities.Converters
{
    public class OpponentInRangeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value)
                return 1;
            return 0.33;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
