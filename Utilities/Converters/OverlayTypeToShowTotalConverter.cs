using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayTypeToShowTotalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            OverlayType overlayType = (OverlayType)value;
            if (overlayType == OverlayType.APM ||
                overlayType == OverlayType.BurstDamageTaken || overlayType == OverlayType.BurstDPS || overlayType == OverlayType.BurstEHPS ||
                overlayType == OverlayType.HealReactionTime || overlayType == OverlayType.TankHealReactionTime)
                return Visibility.Hidden;
            else return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
