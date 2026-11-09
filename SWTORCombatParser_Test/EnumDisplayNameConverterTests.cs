using System.Globalization;
using Avalonia.Data;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities.Converters;
using SWTORCombatParser.Model.Phases;

namespace SWTORCombatParser_Test;

public class EnumDisplayNameConverterTests
{
    [TestCase(PhaseTrigger.Default, "PhaseEnd", "Next phase / combat end")]
    [TestCase(PhaseTrigger.Default, "PhaseStart", "No automatic start")]
    [TestCase(PhaseTrigger.CombatDuration, "PhaseStart", "Elapsed combat time")]
    [TestCase(PhaseTrigger.CombatDuration, "PhaseEnd", "Elapsed phase time")]
    public void PhaseLabelsDistinguishCombatTimeFromPhaseTime(PhaseTrigger value, string context, string expected)
    {
        var converter = new EnumDisplayNameConverter();
        Assert.That(converter.Convert(value, typeof(string), context, CultureInfo.InvariantCulture), Is.EqualTo(expected));
    }
    [TestCase("EffectGained", "Effect Gained")]
    [TestCase("EntityHP", "Entity HP")]
    [TestCase("APM", "APM")]
    public void FormatsLabelsWithoutChangingAcronyms(string value, string expected)
    {
        var converter = new EnumDisplayNameConverter();
        Assert.That(converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture), Is.EqualTo(expected));
    }

    [TestCase("Effect Gained")]
    [TestCase("")]
    [TestCase(null)]
    public void ReverseBindingIgnoresDisplayTextInsteadOfThrowing(string value)
    {
        // Avalonia can request a reverse conversion while attaching/recycling grid
        // cells. Display text must never be written back as a timer trigger.
        var converter = new EnumDisplayNameConverter();
        Assert.That(converter.ConvertBack(value, typeof(TimerKeyType), null, CultureInfo.InvariantCulture),
            Is.SameAs(BindingOperations.DoNothing));
    }
}
