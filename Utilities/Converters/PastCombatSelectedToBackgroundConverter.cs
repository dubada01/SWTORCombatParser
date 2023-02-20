using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class PastCombatSelectedToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch((bool)value)
            {
                case false:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#565956"));
                case true:
                    return System.Windows.Media.Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
