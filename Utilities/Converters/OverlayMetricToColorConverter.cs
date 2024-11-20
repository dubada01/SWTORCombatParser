using SWTORCombatParser.Model.Overlays;
using System;
using System.Globalization;
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
    public class FullOverlayMetricToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not OverlayMetricInfo viewModel)
            {
                return Brushes.Transparent;
            }

            // Assuming the ViewModel has Type and Player properties
            var type = viewModel.Type;
            if (parameter is string secondaryString)
            {
                if (secondaryString == "Secondary")
                    type = viewModel.SecondaryType;
            }
            var player = viewModel.Player;

            if (type == null || player == null)
            {
                return Brushes.Transparent;
            }

            var intendedColor = MetricColorLoader.CurrentMetricBrushDict[type];

            if (!player.IsLocalPlayer)
            {
                return intendedColor;
            }

            return DarkenBrush(intendedColor);
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
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
