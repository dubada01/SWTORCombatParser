using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class BooleanToRaidBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolVal = (bool)value;
            if (boolVal)
                return new SolidColorBrush((Color)ResourceFinder.GetColorFromResourceName("ParticipantHPSColor"));
            else
                return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
