using SWTORCombatParser.ViewModels.Overlays.PvP;
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class MenaceTypeToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var menaceType = (MenaceTypes)value;
            if (menaceType == MenaceTypes.None)
                return Brushes.IndianRed;
            if (menaceType == MenaceTypes.Healer)
                return Brushes.MediumSeaGreen;
            if (menaceType == MenaceTypes.Dps)
                return Brushes.Firebrick;
            return Brushes.Magenta;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
