using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.Utilities.Converters;

public class AbsorbTimerBarVisibilityConverter:IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        TimerKeyType timerTrigger = (TimerKeyType)value;
        if (parameter != null && parameter.ToString()?.ToLower() == "inverted")
        {
            return timerTrigger == TimerKeyType.AbsorbShield ? Visibility.Collapsed : Visibility.Visible;
        }
        return timerTrigger == TimerKeyType.AbsorbShield ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}