using SWTORCombatParser.Model.Overlays;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    class OverlayMetricToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && (bool)parameter == true)
            {
                return Brushes.DarkGoldenrod;
            }
            return MetricColorLoader.CurrentMetricBrushDict[(OverlayType)value];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
