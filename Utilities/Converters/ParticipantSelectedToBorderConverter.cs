﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SWTORCombatParser.Utilities.Converters
{
    public class ParticipantSelectedToBorderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch ((bool)value)
            {
                case true:
                    return new SolidColorBrush(ResourceFinder.GetColorFromResourceName("DarkGreenColor"));
                case false:
                    return System.Windows.Media.Brushes.DimGray;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
