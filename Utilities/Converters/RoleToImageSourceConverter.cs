using SWTORCombatParser.DataStructures.ClassInfos;
using System;
using System.Globalization;
using Avalonia.Data.Converters;


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
