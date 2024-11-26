using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Overlays;

namespace SWTORCombatParser.Utilities.Converters
{
    class OverlayMetricToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter != null && (bool)parameter == true)
            {
                return Brushes.DarkGoldenrod;
            }

            var intendedColor = MetricColorLoader.CurrentMetricBrushDict[(OverlayType)value];
            return intendedColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class FullOverlayMetricToColorConverter : IMultiValueConverter
    {
        // Assuming you are using Avalonia, replace namespaces accordingly

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            // Check if any value is unset
            if (values.Any(v => v == AvaloniaProperty.UnsetValue))
            {
                return Brushes.Transparent; // Return a default value when any binding is unset
            }
            var type = (OverlayType)values[0];
            var secondaryType = (OverlayType)values[1];
            var player = values[2] as Entity; // Replace 'Player' with your actual player class

            // Determine which type to use based on the ConverterParameter
            if (parameter is string secondaryString && secondaryString == "Secondary")
            {
                type = secondaryType;
            }

            if (type == null || player == null)
            {
                return Brushes.Transparent;
            }

            // Retrieve the intended color based on the type
            if (!MetricColorLoader.CurrentMetricBrushDict.TryGetValue(type, out var intendedColor))
            {
                return Brushes.Transparent;
            }

            // Darken the brush if the player is the local player
            if (player.IsLocalPlayer)
            {
                return DarkenBrush(intendedColor);
            }

            return intendedColor;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public static SolidColorBrush DarkenBrush(SolidColorBrush originalBrush, double factor = 0.3)
        {
            if (originalBrush == null)
                throw new ArgumentNullException(nameof(originalBrush));

            if (factor < 0 || factor > 1)
                throw new ArgumentOutOfRangeException(nameof(factor), "Factor must be between 0 and 1.");

            var originalColor = originalBrush.Color;

            // Decrease the RGB values by the factor, ensuring they don't go below 0
            byte r = (byte)Math.Max(0, originalColor.R - originalColor.R * factor);
            byte g = (byte)Math.Max(0, originalColor.G - originalColor.G * factor);
            byte b = (byte)Math.Max(0, originalColor.B - originalColor.B * factor);

            // Create a new color with the adjusted RGB values and the same alpha
            var darkenedColor = Color.FromArgb(originalColor.A, r, g, b);

            // Return a new SolidColorBrush with the darkened color
            return new SolidColorBrush(darkenedColor);
        }

    }
}
