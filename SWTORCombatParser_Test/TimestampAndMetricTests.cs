using System;
using System.Collections.Generic;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overviews;

namespace SWTORCombatParser_Test;

public class TimestampAndMetricTests
{
    [Test]
    public void RectifiesEachMidnightWithGapsInLineNumbers()
    {
        var date = new DateTime(2024, 1, 1);
        var logs = new List<ParsedLogEntry>
        {
            new() { LogLineNumber = 2, TimeStamp = date.AddHours(23).AddMinutes(59) },
            new() { LogLineNumber = 5, TimeStamp = date.AddMinutes(1) },
            new() { LogLineNumber = 8, TimeStamp = date.AddHours(23).AddMinutes(59) },
            new() { LogLineNumber = 20, TimeStamp = date.AddMinutes(1) }
        };

        CombatTimestampRectifier.RectifyTimeStamps(logs);

        Assert.That(logs[0].TimeStamp, Is.EqualTo(date.AddHours(23).AddMinutes(59)));
        Assert.That(logs[1].TimeStamp, Is.EqualTo(date.AddDays(1).AddMinutes(1)));
        Assert.That(logs[2].TimeStamp, Is.EqualTo(date.AddDays(1).AddHours(23).AddMinutes(59)));
        Assert.That(logs[3].TimeStamp, Is.EqualTo(date.AddDays(2).AddMinutes(1)));
    }

    [Test]
    public void SmallOutOfOrderTimeDoesNotAdvanceDay()
    {
        var date = new DateTime(2024, 1, 1);
        var logs = new List<ParsedLogEntry>
        {
            new() { TimeStamp = date.AddHours(10).AddSeconds(2) },
            new() { TimeStamp = date.AddHours(10).AddSeconds(1) }
        };
        CombatTimestampRectifier.RectifyTimeStamps(logs);
        Assert.That(logs[1].TimeStamp, Is.EqualTo(date.AddHours(10).AddSeconds(1)));
    }

    [Test]
    public void MissingMetricReturnsZeroAndExistingValuesAreAveraged()
    {
        var player = new Entity { LogId = 42 };
        var first = new Combat();
        var second = new Combat();
        first.CharacterParticipants[player.LogId] = player;
        second.CharacterParticipants[player.LogId] = player;
        first.TotalHealing[player] = 10;
        second.TotalHealing[player] = 30;

        Assert.That(MetricGetter.GetValueForMetric(OverlayType.RawHealing, new List<Combat> { first, second }, player), Is.EqualTo(20));
        Assert.That(MetricGetter.GetValueForMetric(OverlayType.CleanseCount, new List<Combat> { first, second }, player), Is.Zero);
    }

    [Test]
    public void OverviewPercentageHandlesZeroAndFormatsOnce()
    {
        var row = new CombatInfoInstance { Total = 25, SumTotal = 100 };
        Assert.That(row.PercentOfTotalStr, Is.EqualTo("25%"));
        row.SumTotal = 0;
        Assert.That(row.PercentOfTotal, Is.Zero);
        Assert.That(row.PercentOfTotalStr, Is.EqualTo("0%"));
    }
}
