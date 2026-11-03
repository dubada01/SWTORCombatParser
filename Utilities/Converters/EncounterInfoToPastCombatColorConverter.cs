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
            var setterInfo = ((EncounterInfo, bool, SolidColorBrush))value;
            if (setterInfo.Item1 == null)
                return setterInfo.Item2 ? Brushes.MediumAquamarine : setterInfo.Item3;
            return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("LightGrayGreenColor"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
