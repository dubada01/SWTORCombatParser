using System;
using System.Reflection;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class RecentCombatCacheTests
{
    private static readonly Type CacheType = typeof(Combat).Assembly.GetType("SWTORCombatParser.Model.CombatCaching.RecentCombatCache", true);
    private object _cache;
    private LogState _previousState;
    [SetUp] public void Setup()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.ClearState();
        _cache = Activator.CreateInstance(CacheType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object[] { 2, 10 }, null);
    }
    [TearDown] public void Cleanup() => CombatLogStateBuilder.CurrentState = _previousState;
    private object Call(string name, params object[] args) => CacheType.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_cache, args);
    private static Guid Owner(Combat combat) => (Guid)typeof(Combat).GetProperty("CacheLogStateId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
    private static Combat CombatWith(int count)
    {
        var combat = new Combat();
        for (var i = 0; i < count; i++) combat.AllLogs[i] = new ParsedLogEntry();
        return combat;
    }
    [Test] public void ReturningToRecentCombatReusesItAndEvictsTheLeastRecentlyUsed()
    {
        var a = CombatWith(2); var b = CombatWith(2); var c = CombatWith(2);
        Call("Remember", "a", a); Call("Remember", "b", b);
        Assert.That(Call("Get", "a", Owner(a)), Is.SameAs(a));
        Call("Remember", "c", c);
        Assert.That(Call("Get", "b", Owner(b)), Is.Null);
        Assert.That(Call("Get", "a", Owner(a)), Is.SameAs(a));
        Assert.That(Call("Get", "c", Owner(c)), Is.SameAs(c));
    }
    [Test] public void EventBudgetBoundsLargeFightsAndSkipsAnOversizedFight()
    {
        var a = CombatWith(6); var b = CombatWith(5); var huge = CombatWith(11);
        Call("Remember", "a", a); Call("Remember", "b", b); Call("Remember", "huge", huge);
        Assert.That(Call("Get", "a", Owner(a)), Is.Null);
        Assert.That(Call("Get", "b", Owner(b)), Is.SameAs(b));
        Assert.That(Call("Get", "huge", Owner(huge)), Is.Null);
    }
    [Test] public void ANewParsedLogDiscardsThePreviousWorkingSet()
    {
        var previous = CombatWith(2); Call("Remember", "a", previous);
        CombatLogStateBuilder.ClearState();
        var current = CombatWith(2);
        Assert.That(Call("Get", "a", Owner(current)), Is.Null);
        Call("Remember", "b", current);
        Call("Clear");
        Assert.That(Call("Get", "b", Owner(current)), Is.Null);
    }
    [Test] public void RememberingTheSameFightDoesNotConsumeTheBudgetTwice()
    {
        var a = CombatWith(5); var b = CombatWith(5);
        Call("Remember", "a", a); Call("Remember", "a", a); Call("Remember", "b", b);
        Assert.That(Call("Get", "a", Owner(a)), Is.SameAs(a));
        Assert.That(Call("Get", "b", Owner(b)), Is.SameAs(b));
    }
}
