using System;

using System.Globalization;
using Avalonia.Data.Converters;


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
                return width < CollapseBelowWidth ? false : true;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
