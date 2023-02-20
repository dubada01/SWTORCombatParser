using System;
using System.Globalization;
using System.Windows.Data;

namespace SWTORCombatParser.Utilities.Converters
{
    public class LiveParseActiveToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch((bool)value)
            {
                case true:
                    return "Stop";
                case false:
                    return "Start";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
