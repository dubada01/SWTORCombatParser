using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Avalonia.Controls;

namespace SWTORCombatParser.Utilities.Converters
{
    public class PastCombatSelectedToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((bool)value)
            {
                case false:
                    return (SolidColorBrush)App.Current.FindResource("Gray4Brush");
                case true:
                    return (SolidColorBrush)App.Current.FindResource("Gray6Brush");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
