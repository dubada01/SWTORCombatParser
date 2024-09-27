using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    class LiveParseActiveToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((bool)value)
            {
                case true:
                    //return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF34A547"));
                    return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("ParticipantHPSColor"));
                case false:
                    return (SolidColorBrush)App.Current.FindResource("Gray4Brush");
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

