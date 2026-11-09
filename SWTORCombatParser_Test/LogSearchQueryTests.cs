using System;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.LogReview;

namespace SWTORCombatParser_Test;

public class LogSearchQueryTests
{
    private static ParsedLogEntry Event() => new() {
        SourceInfo = new EntityInfo { Entity = new Entity { Name = "Dread Master", LogId = 101 } },
        TargetInfo = new EntityInfo { Entity = new Entity { Name = "Raider One", LogId = 202 } },
        Ability = "Lightning: Strike (Hard)", AbilityId = 123456,
        Effect = new Effect { EffectId = _7_0LogParsing._damageEffectId, EffectName = "Damage", EffectType = EffectType.Apply },
        Value = new Value { EffectiveDblValue = 12500, DblValue = 20000, WasCrit = true, ValueType = DamageType.energy }
    };
    [TestCase("master raider lightning", true)]
    [TestCase("lightning -type:healing", true)]
    [TestCase("lightning -target:raider", false)]
    [TestCase("\"Lightning: Strike (Hard)\"", true)]
    [TestCase("ability:\"Lightning: Strike (Hard)\"", true)]
    [TestCase("ability:123456 source:101 target:202", true)]
    [TestCase("ability:12345", false)]
    [TestCase("source:Raider", false)]
    [TestCase("target:\"Raider One\" crit:true type:energy", true)]
    [TestCase("crit:false", false)]
    [TestCase("type:healing | type:damage crit:true", true)]
    [TestCase("type:healing | type:damage crit:false", false)]
    [TestCase("value:>=12500 value:<=12500", true)]
    [TestCase("value:>12500", false)]
    [TestCase("value:20000", false)]
    [TestCase("after:1:30 before:90", true)]
    [TestCase("after:90.001", false)]
    [TestCase("before:1:29.999", false)]
    [TestCase("(Hard)", true)]
    [TestCase("[", false)]
    [TestCase("", true)]
    public void QueriesMatchIntendedFields(string query, bool expected) => Assert.That(new LogSearchQuery(query).Matches(Event(), 90), Is.EqualTo(expected));

    [TestCase("ability:")]
    [TestCase("source:\"unfinished")]
    [TestCase("unknown:test")]
    [TestCase("crit:yes")]
    [TestCase("value:NaN")]
    [TestCase("value:>=")]
    [TestCase("after:1:90")]
    [TestCase("before:Infinity")]
    [TestCase("| damage")]
    [TestCase("damage |")]
    public void InvalidQueriesAreExplicit(string query) => Assert.Throws<FormatException>(() => new LogSearchQuery(query));

    [Test]
    public void RegexIsOptInAndInvalidPatternsAreExplicit()
    {
        Assert.That(new LogSearchQuery("Lightning.*Hard", true).Matches(Event(), 90), Is.True);
        Assert.That(new LogSearchQuery("Lightning.*Hard").Matches(Event(), 90), Is.False);
        Assert.Throws<FormatException>(() => new LogSearchQuery("[", true));
    }
    [Test]
    public void QuotedNamesRoundTripEscapes()
    {
        var log = Event(); log.Ability = "A \"quoted\" ability \\ effect";
        Assert.That(new LogSearchQuery("ability:" + LogSearchQuery.Quote(log.Ability)).Matches(log, 0), Is.True);
        log.Ability = "Left | Right";
        Assert.That(new LogSearchQuery("\"|\"").Matches(log, 0), Is.True);
    }
    [TestCase("-0:05", -5)]
    [TestCase("65:01.5", 3901.5)]
    public void RelativeTimesSupportPrePullAndLongCombats(string value, double expected)
    {
        Assert.That(LogSearchQuery.TrySeconds(value, out var seconds), Is.True);
        Assert.That(seconds, Is.EqualTo(expected));
    }
}
