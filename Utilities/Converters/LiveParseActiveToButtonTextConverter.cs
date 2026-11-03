using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    public class LiveParseActiveToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((bool)value)
            {
                case true:
                    return "Stop Parsing";
                case false:
                    return "Start Parsing";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
