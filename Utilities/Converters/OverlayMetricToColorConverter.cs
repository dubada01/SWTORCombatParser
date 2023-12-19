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
            switch ((OverlayType)value)
            {
                case OverlayType.APM:
                    return Brushes.MediumPurple;
                case OverlayType.BurstDPS:
                    return Brushes.Tomato;
                case OverlayType.DPS:
                case OverlayType.Damage:
                    return Brushes.IndianRed;
                case OverlayType.NonEDPS:
                case OverlayType.RawDamage:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d44c73"));
                case OverlayType.FocusDPS:
                    return Brushes.OrangeRed;
                case OverlayType.BurstEHPS:
                    return Brushes.LimeGreen;
                case OverlayType.EHPS:
                case OverlayType.EffectiveHealing:
                    return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("EHPSColor"));
                case OverlayType.HPS:
                case OverlayType.RawHealing:
                    return Brushes.Green;
                case OverlayType.ProvidedAbsorb:
                    return Brushes.CadetBlue;
                case OverlayType.Threat:
                    return Brushes.Orchid;
                case OverlayType.ThreatPerSecond:
                    return Brushes.DarkOrchid;
                case OverlayType.BurstDamageTaken:
                    return Brushes.DarkGoldenrod;
                case OverlayType.DamageTaken:
                    return Brushes.Peru;
                case OverlayType.Mitigation:
                    return Brushes.Sienna;
                case OverlayType.DamageSavedDuringCD:
                    return Brushes.DarkSlateBlue;
                case OverlayType.ShieldAbsorb:
                    return Brushes.SkyBlue;
                case OverlayType.DamageAvoided:
                    return Brushes.DeepSkyBlue;
                case OverlayType.InterruptCount:
                    return Brushes.SteelBlue;
                case OverlayType.CleanseCount:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#357fa1"));
                case OverlayType.CleanseSpeed:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5b8bd9"));
                case OverlayType.HealReactionTime:
                    return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("YellowGrayColor"));
                case OverlayType.HealReactionTimeRatio:
                    return Brushes.Brown;
                case OverlayType.TankHealReactionTime:
                    return Brushes.RosyBrown;
                case OverlayType.CritPercent:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#13ad7d"));
                case OverlayType.SingleTargetDPS:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#d834eb"));
                case OverlayType.SingleTargetEHPS:
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00c497"));
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
