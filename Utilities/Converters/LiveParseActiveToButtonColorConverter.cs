using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class LiveParseActiveToButtonColorConverter:IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((bool)value)
            {
                case true:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFa53434"));
                case false:
                    return new SolidColorBrush((Color)ResourceFinder.GetColorFromResourceName("ParticipantHPSColor"));
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
