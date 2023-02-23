using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    internal class BooleanToMetricOptionBackgroundConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverted = parameter != null && ((string)parameter).ToLower() == "inverted";

            if (isInverted)
            {
                if (!(bool)value)
                    return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray11"));
                return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray4"));
            }

            if ((bool)value)
                return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray11"));
            return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("Gray4"));
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
