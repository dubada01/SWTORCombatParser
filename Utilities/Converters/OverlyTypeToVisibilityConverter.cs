using SWTORCombatParser.Model.Overlays;
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters
{
    class OverlyTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((OverlayType)value)
            {
                case OverlayType.None:
                    return false;
                default:
                    return true;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
