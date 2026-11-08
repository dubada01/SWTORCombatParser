using System;
using System.Collections.Generic;
using NUnit.Framework;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;

namespace SWTORCombatParser_Test;

public class EncounterSessionGroupingTests
{
    [TestCase(EncounterType.Arena)]
    [TestCase(EncounterType.Warzone)]
    [TestCase(EncounterType.Flashpoint)]
    public void SameVisitStaysTogetherButAnotherVisitStartsANewSession(EncounterType type)
    {
        EncounterInfo Visit() => new()
        {
            Name = "Same map", EncounterType = type,
            BossInfos = type == EncounterType.Flashpoint
                ? new List<BossInfo> { new() { EncounterName = "Boss" } } : new()
        };
        var first = Visit();
        var start = new DateTime(2026, 11, 7);
        Assert.That(EncounterSessionGrouping.StartsNewSession(first, first, start, start), Is.False);
        Assert.That(EncounterSessionGrouping.StartsNewSession(first, Visit(), start, start.AddHours(1)), Is.True);
    }

    [Test]
    public void UnknownLocationsDoNotCreateASeparateSessionForEveryCombat()
    {
        var first = new EncounterInfo { Name = "Unknown Encounter" };
        Assert.That(EncounterSessionGrouping.StartsNewSession(first, new EncounterInfo { Name = first.Name }, DateTime.MinValue, DateTime.MaxValue), Is.False);
        Assert.That(EncounterSessionGrouping.StartsNewSession(null, first, DateTime.MinValue, DateTime.MinValue), Is.True);
    }

    [Test]
    public void DuplicateAreaRecordsShareVisitButTravelWithoutCombatStillBreaksIt()
    {
        var start = new DateTime(2026, 11, 7);
        var entries = new Dictionary<DateTime, EncounterInfo>
        {
            [start] = new() { Name = "Arena", EncounterType = EncounterType.Arena },
            [start.AddMilliseconds(56)] = new() { Name = "Arena", EncounterType = EncounterType.Arena },
            [start.AddMinutes(5)] = new() { Name = "Fleet" },
            [start.AddMinutes(6)] = new() { Name = "Arena", EncounterType = EncounterType.Arena }
        };
        Assert.That(EncounterSessionGrouping.VisitStart(entries, start.AddMinutes(1)), Is.EqualTo(start));
        Assert.That(EncounterSessionGrouping.VisitStart(entries, start.AddMinutes(7)), Is.EqualTo(start.AddMinutes(6)));
    }
}
