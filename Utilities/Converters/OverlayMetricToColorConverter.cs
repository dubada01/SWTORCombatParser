using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    class OverlayMetricToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((OverlayType)value)
            {
                case OverlayType.DPS:
                    return Brushes.IndianRed;
                case OverlayType.FocusDPS:
                    return Brushes.OrangeRed;
                case OverlayType.Healing:
                    return Brushes.LimeGreen;
                case OverlayType.Sheilding:
                    return Brushes.CornflowerBlue;
                case OverlayType.Threat:
                    return Brushes.Orchid;
                case OverlayType.DTPS:
                    return Brushes.Peru;
                default:
                    return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
