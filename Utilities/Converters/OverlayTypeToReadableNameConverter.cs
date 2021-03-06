using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SWTORCombatParser.Utilities.Converters
{
    public class OverlayTypeToReadableNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            OverlayType oType = (OverlayType)value;
            switch (oType)
            {
                case OverlayType.None:
                    return "None";
                case OverlayType.DPS:
                    return "DPS";
                case OverlayType.BurstDPS:
                    return "Burst DPS";
                case OverlayType.FocusDPS:
                    return "Boss DPS";
                case OverlayType.HPS:
                    return "HPS";
                case OverlayType.EHPS:
                    return "EHPS + Shield";
                case OverlayType.BurstEHPS:
                    return "Burst EHPS";
                case OverlayType.HealReactionTime:
                    return "# of <2sec Reactions";
                case OverlayType.TankHealReactionTime:
                    return "Heal Reaction: Tanks";
                case OverlayType.Mitigation:
                    return "Damage Mitigation";
                case OverlayType.ShieldAbsorb:
                    return "Tank Shielding";
                case OverlayType.DamageAvoided:
                    return "Damage Avoided";
                case OverlayType.Shielding:
                    return "Absorb";
                case OverlayType.APM:
                    return "APM";
                case OverlayType.InterruptCount:
                    return "Interrupt Count";
                default:
                    return oType.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
