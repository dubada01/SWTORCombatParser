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
                case OverlayType.EHPS:
                    return Brushes.LimeGreen;
                case OverlayType.SPS:
                    return Brushes.CornflowerBlue;
                case OverlayType.TPS:
                    return Brushes.Orchid;
                case OverlayType.DTPS:
                    return Brushes.Peru;
                case OverlayType.CompanionDPS:
                    return new SolidColorBrush(Brushes.IndianRed.Color.Lerp(Color.FromRgb(255,255,255), 0.33f));
                case OverlayType.CompanionEHPS:
                    return new SolidColorBrush(Brushes.LimeGreen.Color.Lerp(Color.FromRgb(255, 255, 255), 0.33f));
                case OverlayType.HealthDeficit:
                    return Brushes.DimGray;
                default:
                    return Brushes.Gray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
