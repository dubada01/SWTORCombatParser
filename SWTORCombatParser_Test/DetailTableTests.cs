using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Overviews;

namespace SWTORCombatParser_Test;

public class DetailTableTests
{
    private static readonly Entity Player = new() { Name = "Player", LogId = 1, Id = 1, IsCharacter = true };
    private static readonly Entity Boss = new() { Name = "Boss", LogId = 2, Id = 2, IsBoss = true };
    private static Combat Fight() => new() { StartTime = new DateTime(2026, 11, 8), EndTime = new DateTime(2026, 11, 8).AddSeconds(10) };
    private static ParsedLogEntry Event(string ability, double amount, bool crit = false, Entity source = null, Entity target = null) => new()
    {
        SourceInfo = new EntityInfo { Entity = source ?? Player }, TargetInfo = new EntityInfo { Entity = target ?? Boss },
        Ability = ability, Value = new Value { EffectiveDblValue = amount, DblValue = amount + 50, WasCrit = crit, ValueType = DamageType.energy }
    };
    private static List<CombatInfoInstance> Rows(Combat combat, OverviewDataType type, SortingOption grouping = SortingOption.ByAbility, CancellationToken token = default) =>
        (List<CombatInfoInstance>)typeof(TableInstanceViewModel).GetMethod("BuildRows", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { combat, Player, type, grouping, token })!;

    [Test]
    public void DamageGroupsPreserveFractionalEffectiveAmountsAndEventStatistics()
    {
        var fight = Fight();
        fight.OutgoingDamageLogs[Player] = new(new[] { Event("A", 100.5), Event("A", 200.25, true), Event("B", 50.25) });
        var rows = Rows(fight, OverviewDataType.Damage);
        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(r => r.SortItem), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(rows[0].Total, Is.EqualTo(300.75));
            Assert.That(rows[0].Average, Is.EqualTo(150.375));
            Assert.That(rows[0].Rate, Is.EqualTo(30.075));
            Assert.That(rows[0].Count, Is.EqualTo(2));
            Assert.That(rows[0].Max, Is.EqualTo(100.5));
            Assert.That(rows[0].MaxCrit, Is.EqualTo(200.25));
            Assert.That(rows[0].CritPercent, Is.EqualTo(50));
            Assert.That(rows.Sum(r => r.PercentOfTotal), Is.EqualTo(100).Within(0.00001));
        });
    }

    [TestCase(SortingOption.ByAbility, 2)]
    [TestCase(SortingOption.BySource, 1)]
    [TestCase(SortingOption.ByTarget, 1)]
    public void GroupingKeepsTotalsIncludingDifferentInstancesOfTheSameNpc(SortingOption grouping, int groups)
    {
        var fight = Fight();
        var secondBoss = new Entity { Name = "Boss", LogId = 2, Id = 3 };
        fight.OutgoingDamageLogs[Player] = new(new[] { Event("A", 100), Event("B", 200, target: secondBoss) });
        var rows = Rows(fight, OverviewDataType.Damage, grouping);
        Assert.That(rows.Count, Is.EqualTo(groups));
        Assert.That(rows.Sum(r => r.Total), Is.EqualTo(300));
        Assert.That(rows.Sum(r => r.Count), Is.EqualTo(2));
    }

    [Test]
    public void HealingIncludesShieldsAndIncomingCategoriesUseTheTarget()
    {
        var fight = Fight();
        fight.OutgoingHealingLogs[Player] = new(new[] { Event("Heal", 150) });
        fight.ShieldingProvidedLogs[Player] = new(new[] { Event("Barrier", 100) });
        fight.IncomingHealingLogs[Player] = new(new[] { Event("Incoming heal", 75, source: Boss, target: Player) });
        fight.IncomingDamageLogs[Player] = new(new[] { Event("Incoming hit", 125, source: Boss, target: Player) });
        Assert.That(Rows(fight, OverviewDataType.Healing).Sum(r => r.Total), Is.EqualTo(250));
        Assert.That(Rows(fight, OverviewDataType.HealingReceived).Single().Total, Is.EqualTo(75));
        Assert.That(Rows(fight, OverviewDataType.DamageTaken, SortingOption.BySource).Single().SortItem, Is.EqualTo("Boss"));
        Assert.That(Rows(fight, OverviewDataType.DamageTaken).Single().Total, Is.EqualTo(125));
    }

    [Test]
    public void EmptyAndZeroDurationCombatsStayFinite()
    {
        var fight = Fight(); fight.EndTime = fight.StartTime;
        Assert.That(Rows(fight, OverviewDataType.Damage), Is.Empty);
        fight.OutgoingDamageLogs[Player] = new(new[] { Event("Miss", 0) });
        var row = Rows(fight, OverviewDataType.Damage).Single();
        Assert.That(row.Rate, Is.Zero); Assert.That(row.PercentOfTotal, Is.Zero);
    }

    [Test]
    public void CancelledBuildDoesNotPublishPartialResults()
    {
        var fight = Fight(); fight.OutgoingDamageLogs[Player] = new(new[] { Event("Hit", 100) });
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var error = Assert.Throws<TargetInvocationException>(() => Rows(fight, OverviewDataType.Damage, token: cancelled.Token));
        Assert.That(error.InnerException, Is.InstanceOf<OperationCanceledException>());
    }
}
