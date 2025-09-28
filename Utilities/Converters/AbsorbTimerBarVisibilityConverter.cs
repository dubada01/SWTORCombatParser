using SWTORCombatParser.DataStructures;
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SWTORCombatParser.Utilities.Converters;

public class AbsorbTimerBarVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is TimerKeyType timerTrigger)
        {
            if (parameter != null && parameter.ToString()?.ToLower() == "inverted")
            {
                return timerTrigger != TimerKeyType.AbsorbShield;
            }
            return timerTrigger == TimerKeyType.AbsorbShield;
        }

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}