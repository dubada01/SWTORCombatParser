using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace SWTORCombatParser.Utilities.Converters
{
    public class RoleToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((Role)value)
            {
                case Role.DPS:
                    return "../../resources/dpsIcon.png";
                case Role.Tank:
                    return "../../resources/tankIcon.png";
                case Role.Healer:
                    return "../../resources/healingIcon.png";
                case Role.Unknown:
                    return "";
                default:
                    return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
