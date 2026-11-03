using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.Utilities.Converters
{
    public class BooleanToEncounterColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            EncounterInfo encounter = (EncounterInfo)value;
            if (encounter.IsBossEncounter)
                return new SolidColorBrush(Colors.DarkGoldenrod);
            if(encounter.IsPvpEncounter)
                return new SolidColorBrush(Colors.OrangeRed);
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
