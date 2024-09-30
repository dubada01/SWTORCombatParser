using SWTORCombatParser.Model.Overlays;
using System;
using System.Globalization;
using Avalonia.Data.Converters;


namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayTypeToShowTotalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            OverlayType overlayType = (OverlayType)value;
            if (overlayType == OverlayType.APM ||
                overlayType == OverlayType.BurstDamageTaken || overlayType == OverlayType.BurstDPS ||
                overlayType == OverlayType.BurstEHPS ||
                overlayType == OverlayType.HealReactionTime || overlayType == OverlayType.TankHealReactionTime)
                return false;
            else return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
