using System.Collections.Generic;
using System.Collections.Concurrent;
using Avalonia.Media;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Challenges;

namespace SWTORCombatParser_Test;

public class ChallengeMetricTests
{
    [TestCase(ChallengeType.DamageOut, false, null, 30)]
    [TestCase(ChallengeType.DamageOut, true, "", 300)]
    [TestCase(ChallengeType.DamageIn, false, "", 30)]
    [TestCase(ChallengeType.DamageIn, true, null, 300)]
    [TestCase(ChallengeType.DamageOut, true, "ANY", 300)]
    [TestCase(ChallengeType.DamageIn, true, "any", 300)]
    public void UnfilteredDamageIncludesAllAbilitiesAndEntities(ChallengeType type, bool total, string filter, double expected)
    {
        var player = new Entity { LogId = 1 };
        var logs = new ConcurrentQueue<ParsedLogEntry>(new[] {
            new ParsedLogEntry { AbilityId = 1, Value = new Value { EffectiveDblValue = 100 } },
            new ParsedLogEntry { AbilityId = 2, Value = new Value { EffectiveDblValue = 200 } }
        });
        var combat = new Combat { DurationOverride = 10000 };
        combat.OutgoingDamageLogs[player] = logs;
        combat.IncomingDamageLogs[player] = logs;
        var challenge = new Challenge { Value = filter, ChallengeSource = filter, ChallengeTarget = filter, UseRawValues = total };
        Assert.That(ChallengeMetrics.GetValueForChallenege(type, combat, player, challenge, new Combat()), Is.EqualTo(expected));
    }

    [TestCase(ChallengeType.DamageIn)]
    [TestCase(ChallengeType.DamageOut)]
    public void EmptyUnfilteredDamageIsZero(ChallengeType type)
    {
        Assert.That(ChallengeMetrics.GetValueForChallenege(type, new Combat(), new Entity { LogId = 1 }, new Challenge(), new Combat()), Is.Zero);
    }

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
