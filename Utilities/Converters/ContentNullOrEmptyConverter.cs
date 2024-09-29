using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SWTORCombatParser.Utilities.Converters
{
    public class ContentNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            {
                return false; // Return false to collapse the tooltip
            }

            return true; // Keep the tooltip visible
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}