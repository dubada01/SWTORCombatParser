using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    class LogLatencyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var latency = (double)value;
            if (latency < 2)
            {
                return new SolidColorBrush(Colors.LightGreen);
            }
            if (latency < 3)
            {
                return new SolidColorBrush(Colors.YellowGreen);
            }
            if (latency < 3.5)
            {
                return new SolidColorBrush(Colors.Orange);
            }
            if (latency >= 3.5)
            {
                return new SolidColorBrush(Colors.Tomato);
            }
            return new SolidColorBrush(Colors.WhiteSmoke);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
