using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class IndexToRowColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((int)value % 2 == 0)
            {
                case true:
                    return new SolidColorBrush(Colors.WhiteSmoke);
                case false:
                    return new SolidColorBrush(Colors.White);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
