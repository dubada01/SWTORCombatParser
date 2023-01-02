using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    class LogLatencyToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var latency = (double)value;
            if(latency < 2)
            {
                return Brushes.LightGreen;
            }
            if(latency < 3)
            {
                return Brushes.YellowGreen;
            }
            if(latency < 3.5)
            {
                return Brushes.Orange;
            }
            if(latency >= 3.5)
            {
                return Brushes.Tomato;
            }
            return Brushes.WhiteSmoke;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
