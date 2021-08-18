using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace SWTORCombatParser.Utilities.Converters
{
    public class PastCombatSelectedToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch((bool)value)
            {
                case true:
                    return System.Windows.Media.Brushes.LightSteelBlue;
                case false:
                    return System.Windows.Media.Brushes.LightGray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
