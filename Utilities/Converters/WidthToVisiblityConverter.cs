using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace SWTORCombatParser.Utilities.Converters
{
    internal class WidthToVisiblityConverter:IValueConverter
    {
        public double CollapseBelowWidth { get; set; } = 800; // Default width threshold

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // Collapse the TextBlock if the window width is below a certain threshold.
                return width < CollapseBelowWidth ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
