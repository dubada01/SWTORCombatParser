using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.Utilities.Converters
{
    public class EncounterInfoToPastCombatColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var setterInfo = ((EncounterInfo,bool))value;
            if (setterInfo.Item1 == null)
                return setterInfo.Item2 ? Brushes.MediumAquamarine : Brushes.WhiteSmoke;
            return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("LightGrayGreenColor"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
