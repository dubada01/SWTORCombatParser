using SWTORCombatParser.Model.Overlays;
using System;
using System.Globalization;
using Avalonia.Data.Converters;


namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayTypeToShowTotalConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is OverlayType overlayType)
            {
                if (overlayType == OverlayType.APM ||
                    overlayType == OverlayType.BurstDamageTaken || overlayType == OverlayType.BurstDPS ||
                    overlayType == OverlayType.BurstEHPS ||
                    overlayType == OverlayType.HealReactionTime || overlayType == OverlayType.TankHealReactionTime)
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
