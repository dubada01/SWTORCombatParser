using System.Collections.Generic;
using Avalonia.Media;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.ViewModels.Challenges;

namespace SWTORCombatParser_Test;

public class ChallengeMetricTests
{
    [Test]
    public void MissingInterruptMetricIsZero()
    {
        var value = ChallengeMetrics.GetValueForChallenege(ChallengeType.InterruptCount,
            new Combat(), new Entity { LogId = 1 }, new Challenge(), new Combat());
        Assert.That(value, Is.Zero);
    }

    [Test]
    public void UpdatingBarNotifiesDependentValuesAndClampsLength()
    {
        var bar = new ChallengeOverlayMetricInfo(new SolidColorBrush());
        var changed = new List<string>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        bar.Value = 100;
        bar.RelativeLength = 2;
        Assert.That(bar.RelativeLength, Is.EqualTo(1));
        Assert.That(changed, Does.Contain(nameof(bar.TotalValue)));
        Assert.That(changed, Does.Contain(nameof(bar.InfoText)));
        bar.RelativeLength = double.NaN;
        Assert.That(bar.RelativeLength, Is.Zero);
    }
}
