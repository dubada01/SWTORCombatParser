using SWTORCombatParser.DataStructures.EncounterInfo;
using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class EncounterInfoToPastCombatColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var setterInfo = ((bool isBossCombat, bool wasBossKilled, bool isPvPCombat, SolidColorBrush pvpBrush))value;

            if (setterInfo.isPvPCombat)
                return setterInfo.pvpBrush;

            if (setterInfo.isBossCombat)
                return setterInfo.wasBossKilled ? Brushes.MediumAquamarine : Brushes.IndianRed;

            return Brushes.WhiteSmoke;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
