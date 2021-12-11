﻿using SWTORCombatParser.Model.Overlays;
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
                case OverlayType.APM:
                    return Brushes.MediumPurple;
                case OverlayType.BurstDPS:
                    return Brushes.Red;
                case OverlayType.DPS:
                    return Brushes.IndianRed;
                case OverlayType.FocusDPS:
                    return Brushes.OrangeRed;
                case OverlayType.BurstEHPS:
                    return Brushes.LimeGreen;
                case OverlayType.EHPS:
                    return Brushes.Green;
                case OverlayType.HPS:
                    return Brushes.ForestGreen;
                case OverlayType.Tank_Shielding:
                    return Brushes.CornflowerBlue;
                case OverlayType.Shielding:
                    return Brushes.CadetBlue;
                case OverlayType.Threat:
                    return Brushes.Orchid;
                case OverlayType.BurstDamageTaken:
                    return Brushes.SaddleBrown;
                case OverlayType.DamageTaken:
                    return Brushes.Peru;
                case OverlayType.CompanionDPS:
                    return new SolidColorBrush(Brushes.IndianRed.Color.Lerp(Color.FromRgb(255,255,255), 0.33f));
                case OverlayType.CompanionEHPS:
                    return new SolidColorBrush(Brushes.LimeGreen.Color.Lerp(Color.FromRgb(255, 255, 255), 0.33f));
                case OverlayType.PercentOfFightBelowFullHP:
                    return Brushes.DimGray;
                case OverlayType.Mitigation:
                    return Brushes.Sienna;
                case OverlayType.ShieldAbsorb:
                    return Brushes.SkyBlue;
                case OverlayType.DamageAvoided:
                    return Brushes.DeepSkyBlue;
                case OverlayType.InterruptCount:
                    return Brushes.SteelBlue;
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
