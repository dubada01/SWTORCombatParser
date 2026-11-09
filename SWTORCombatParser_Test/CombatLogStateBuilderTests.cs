using System;
using System.Linq;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatLogStateBuilderTests
{
    private static readonly DateTime Start = new(2026, 11, 7, 12, 0, 0);
    private const ulong EffectId = 42;
    private LogState _previousState;
    private Entity _source;
    private Entity _target;

    [SetUp]
    public void Setup()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.ClearState();
        _source = new Entity { Name = "Source", Id = 1, LogId = 1, IsCharacter = true };
        _target = new Entity { Name = "Target", Id = 2, LogId = 2 };
    }

    [TearDown]
    public void Cleanup() => CombatLogStateBuilder.CurrentState = _previousState;

    [Test]
    public void RefreshChargesAndRemovalKeepHistoricalIntervalsAvailableForScopeQueries()
    {
        Update(EffectType.Apply, 0, charges: 3);
        var original = History().Single();
        Update(EffectType.ModifyCharges, 1, charges: 2);
        Update(EffectType.Apply, 2, charges: 4);
        var refreshed = History().Single(modifier => !modifier.Complete);
        Update(EffectType.ModifyCharges, 3, charges: 1);
        Update(EffectType.Remove, 4);
        // An unmatched charge/remove must not change a completed interval.
        Update(EffectType.ModifyCharges, 5, charges: 8);
        Update(EffectType.Remove, 6);

        var scoped = CombatLogStateBuilder.CurrentState
            .GetEffectsWithTarget(Start.AddSeconds(1), Start.AddSeconds(3), _target)
            .OrderBy(modifier => modifier.StartTime).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(History(), Has.Length.EqualTo(2));
            Assert.That(original.Complete, Is.True);
            Assert.That(original.StopTime, Is.EqualTo(Start.AddSeconds(2)));
            Assert.That(original.GetEffectStackForTimestamp(Start.AddSeconds(1)), Is.EqualTo(2));
            Assert.That(refreshed.Complete, Is.True);
            Assert.That(refreshed.StopTime, Is.EqualTo(Start.AddSeconds(4)));
            Assert.That(refreshed.GetEffectStackForTimestamp(Start.AddSeconds(5)), Is.EqualTo(1));
            Assert.That(scoped, Has.Count.EqualTo(2));
            Assert.That(scoped[0].StartTime, Is.EqualTo(Start.AddSeconds(1)));
            Assert.That(scoped[0].StopTime, Is.EqualTo(Start.AddSeconds(2)));
            Assert.That(scoped[1].StartTime, Is.EqualTo(Start.AddSeconds(2)));
            Assert.That(scoped[1].StopTime, Is.EqualTo(Start.AddSeconds(3)));
        });
    }

    [Test]
    public void SameEffectKeepsDifferentSourceAndTargetInstancesIndependent()
    {
        // Separate NPC instances can share the same LogId; matching uses reference identity.
        var otherSource = new Entity { Name = "Other source", Id = 3, LogId = _source.LogId, IsCharacter = true };
        var otherTarget = new Entity { Name = "Other target", Id = 4, LogId = _target.LogId };
        Update(EffectType.Apply, 0);
        Update(EffectType.Apply, 1, source: otherSource);
        Update(EffectType.Apply, 2, target: otherTarget);
        Update(EffectType.Remove, 3);

        Assert.Multiple(() =>
        {
            Assert.That(History(), Has.Length.EqualTo(3));
            Assert.That(History().Single(modifier => ReferenceEquals(modifier.Source, _source) &&
                ReferenceEquals(modifier.Target, _target)).Complete, Is.True);
            Assert.That(History().Single(modifier => ReferenceEquals(modifier.Source, otherSource)).Complete, Is.False);
            Assert.That(History().Single(modifier => ReferenceEquals(modifier.Target, otherTarget)).Complete, Is.False);
        });
    }

    [Test]
    public void UnknownSourceCanChangeChargesAndRemoveAnEffectOnItsTarget()
    {
        Update(EffectType.Apply, 0, charges: 3);
        var modifier = History().Single();
        var unknown = new Entity();
        Update(EffectType.ModifyCharges, 1, source: unknown, charges: 2);
        Update(EffectType.Remove, 2, source: unknown);

        Assert.Multiple(() =>
        {
            Assert.That(modifier.ChargesAtTime[Start.AddSeconds(1)], Is.EqualTo(2));
            Assert.That(modifier.Complete, Is.True);
            Assert.That(modifier.StopTime, Is.EqualTo(Start.AddSeconds(2)));
        });
    }

    [Test]
    public void UnknownSourceMatchesTheLatestApplicationAcrossNpcAttempts()
    {
        // The Dread Council log applies Approaching Death on the same player in
        // three attempts but records only the last removal, without a source.
        _source = new Entity { Name = "Tyrans", Id = 5621003650764, LogId = 3273997735165952 };
        _target = new Entity { Name = "Zoomz", Id = 686519019581980, LogId = 686519019581980, IsCharacter = true };
        var secondSource = new Entity { Name = "Tyrans", Id = 5621003775831, LogId = _source.LogId };
        var currentSource = new Entity { Name = "Tyrans", Id = 5621004089494, LogId = _source.LogId };
        Update(EffectType.Apply, 0);
        Update(EffectType.Apply, 237, source: secondSource);
        Update(EffectType.Apply, 1226, source: currentSource, charges: 3);
        Update(EffectType.ModifyCharges, 1228, source: new Entity(), charges: 2);
        Update(EffectType.Remove, 1233, source: new Entity());

        var current = History().Single(modifier => ReferenceEquals(modifier.Source, currentSource));
        Assert.Multiple(() =>
        {
            Assert.That(current.Complete, Is.True);
            Assert.That(current.StopTime, Is.EqualTo(Start.AddSeconds(1233)));
            Assert.That(current.ChargesAtTime[Start.AddSeconds(1228)], Is.EqualTo(2));
            Assert.That(History().Where(modifier => !ReferenceEquals(modifier, current)).All(modifier =>
                !modifier.Complete && modifier.StopTime == DateTime.MinValue && modifier.ChargesAtTime.Count == 1), Is.True);
            Assert.That(CombatLogStateBuilder.CurrentState.GetEffectsWithTarget(Start.AddSeconds(300),
                Start.AddSeconds(400), _target), Is.Empty, "The last removal must not extend a stale earlier attempt's effect.");
        });
    }

    [Test]
    public void UnknownSourceUsesStableInstanceIdsWhenApplicationsHaveTheSameTimestamp()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            CombatLogStateBuilder.ClearState();
            var otherSource = new Entity { Name = "Other source", Id = 3, LogId = _source.LogId };
            // Reverse insertion order as well as regenerating random modifier keys.
            Update(EffectType.Apply, 0, source: attempt % 2 == 0 ? _source : otherSource);
            Update(EffectType.Apply, 0, source: attempt % 2 == 0 ? otherSource : _source);
            Update(EffectType.Remove, 1, source: new Entity());

            Assert.That(History().Single(modifier => modifier.Complete).Source, Is.SameAs(_source));
        }
    }

    [Test]
    public void UnknownSourceDoesNotCloseAnApplicationAfterItsTimestamp()
    {
        Update(EffectType.Apply, 10);
        Update(EffectType.Remove, 5, source: new Entity());
        Assert.That(History().Single().Complete, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SameTimestampSeverForceRemovalsCloseBothCastersRegardlessOfSourceOrder(bool unknownFirst)
    {
        // D-09 Corruption Droid receives Pxls' Sever Force, then Ji'chiri's.
        // At death the log removes the unknown caster first and Ji'chiri second.
        var pxls = new Entity { Name = "Pxls", Id = 690126756037098, LogId = 690126756037098, IsCharacter = true };
        var jichiri = new Entity { Name = "Ji'chiri", Id = 689720753673708, LogId = 689720753673708, IsCharacter = true };
        _target = new Entity { Name = "D-09 Corruption Droid", Id = 18998000003414, LogId = 3298384559472640 };
        Update(EffectType.Apply, 0, source: pxls);
        Update(EffectType.Apply, 1, source: jichiri);
        Update(EffectType.Remove, 15, source: unknownFirst ? new Entity() : jichiri);
        Update(EffectType.Remove, 15, source: unknownFirst ? jichiri : new Entity());

        Assert.Multiple(() =>
        {
            Assert.That(History(), Has.Length.EqualTo(2));
            Assert.That(History().All(modifier => modifier.Complete && modifier.StopTime == Start.AddSeconds(15)), Is.True);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void MultipleUnknownRemovalsAreReassignedOnceForEachLaterExactSource(bool unknownFirst)
    {
        var sources = Enumerable.Range(10, 4).Select(id => new Entity { Id = id, LogId = id, Name = "Source " + id }).ToArray();
        for (var index = 0; index < sources.Length; index++)
            Update(EffectType.Apply, index, source: sources[index]);
        if (unknownFirst)
        {
            Update(EffectType.Remove, 10, source: new Entity());
            Update(EffectType.Remove, 10, source: new Entity());
        }
        Update(EffectType.Remove, 10, source: sources[3]);
        Update(EffectType.Remove, 10, source: sources[2]);
        if (!unknownFirst)
        {
            Update(EffectType.Remove, 10, source: new Entity());
            Update(EffectType.Remove, 10, source: new Entity());
        }

        Assert.That(History().All(modifier => modifier.Complete && modifier.StopTime == Start.AddSeconds(10)), Is.True);
    }

    [Test]
    public void RepeatedUnknownAndExactRemovalWithoutAnotherCandidateDoesNotCreateAnInterval()
    {
        Update(EffectType.Apply, 0);
        Update(EffectType.Remove, 10, source: new Entity());
        Update(EffectType.Remove, 10);
        Update(EffectType.Remove, 10);

        Assert.That(History(), Has.Length.EqualTo(1));
        Assert.That(History().Single().StopTime, Is.EqualTo(Start.AddSeconds(10)));
    }

    [Test]
    public void ExactRemovalAtANewTimestampCannotReassignAPreviousUnknownRemoval()
    {
        var laterSource = new Entity { Name = "Later caster", Id = 3, LogId = 3 };
        Update(EffectType.Apply, 0);
        Update(EffectType.Apply, 1, source: laterSource);
        Update(EffectType.Remove, 10, source: new Entity());
        Update(EffectType.Remove, 11, source: laterSource);

        Assert.Multiple(() =>
        {
            Assert.That(History().Single(modifier => modifier.Source == _source).Complete, Is.False);
            Assert.That(History().Single(modifier => modifier.Source == laterSource).StopTime, Is.EqualTo(Start.AddSeconds(10)));
        });
    }

    [Test]
    public void UnknownRemovalAssignmentsCannotBeBorrowedByAnotherTargetOrEffect()
    {
        var laterSource = new Entity { Name = "Later caster", Id = 3, LogId = 3 };
        var otherTarget = new Entity { Name = "Other target", Id = 4, LogId = 4 };
        Update(EffectType.Apply, 0);
        Update(EffectType.Apply, 1, source: laterSource);
        Update(EffectType.Remove, 10, source: new Entity());
        Update(EffectType.Remove, 10, source: laterSource, target: otherTarget);
        Update(EffectType.Remove, 10, source: laterSource, effectId: EffectId + 1);

        Assert.That(History().Single(modifier => modifier.Source == _source).Complete, Is.False);
    }

    [Test]
    public void UnknownForceSuppressionChargesContinueTheLatestCastersChargeSequence()
    {
        var pxls = new Entity { Name = "Pxls", Id = 690126756037098, LogId = 690126756037098, IsCharacter = true };
        Update(EffectType.Apply, 0, charges: 15);
        Update(EffectType.Apply, 3, source: pxls, charges: 15);
        Update(EffectType.ModifyCharges, 11, source: pxls, charges: 6);
        Update(EffectType.ModifyCharges, 12, charges: 5);
        Update(EffectType.ModifyCharges, 13, source: new Entity(), charges: 5);
        Update(EffectType.ModifyCharges, 14, charges: 4);
        Update(EffectType.ModifyCharges, 15, source: new Entity(), charges: 4);
        var earlier = History().Single(modifier => modifier.Source == _source);
        var later = History().Single(modifier => modifier.Source == pxls);

        Assert.Multiple(() =>
        {
            Assert.That(later.ChargesAtTime[Start.AddSeconds(13)], Is.EqualTo(5));
            Assert.That(later.ChargesAtTime[Start.AddSeconds(15)], Is.EqualTo(4));
            Assert.That(earlier.ChargesAtTime.ContainsKey(Start.AddSeconds(13)), Is.False);
            Assert.That(earlier.ChargesAtTime.ContainsKey(Start.AddSeconds(15)), Is.False);
        });
    }

    [Test]
    public void ExistingHistoryIsAvailableWhenTheStateBuilderFirstUpdatesAState()
    {
        var modifier = new CombatModifier
        {
            EffectId = EffectId, Source = _source, Target = _target, StartTime = Start
        };
        CombatLogStateBuilder.CurrentState.Modifiers[EffectId] = new();
        CombatLogStateBuilder.CurrentState.Modifiers[EffectId][Guid.NewGuid()] = modifier;
        Update(EffectType.Remove, 1);

        Assert.That(modifier.Complete, Is.True);
        Assert.That(modifier.StopTime, Is.EqualTo(Start.AddSeconds(1)));
    }

    [Test]
    public void SwitchingAndClearingStateDoNotReuseActiveModifiersFromAnotherLog()
    {
        Update(EffectType.Apply, 0);
        var firstState = CombatLogStateBuilder.CurrentState;
        var firstModifier = History().Single();
        CombatLogStateBuilder.CurrentState = new LogState();
        Update(EffectType.Apply, 1);
        var secondModifier = History().Single();
        Update(EffectType.Remove, 2);

        Assert.That(firstModifier.Complete, Is.False);
        Assert.That(secondModifier.Complete, Is.True);

        CombatLogStateBuilder.CurrentState = firstState;
        Update(EffectType.Remove, 3);
        Assert.That(firstModifier.StopTime, Is.EqualTo(Start.AddSeconds(3)));

        CombatLogStateBuilder.ClearState();
        Update(EffectType.Remove, 4);
        Assert.That(History(), Is.Empty);
        Update(EffectType.Apply, 5);
        Assert.That(History().Single().StartTime, Is.EqualTo(Start.AddSeconds(5)));
    }

    [Test]
    public void RepeatedEntitiesPreserveFirstSeenDeathAndRevivalHistory()
    {
        Update(EffectType.Apply, 0);
        Update(EffectType.Apply, 1);
        Update(EffectType.Apply, 2, effectId: _7_0LogParsing.DeathCombatId);
        Update(EffectType.Apply, 3, target: _source, effectId: _7_0LogParsing.DeathCombatId);
        Update(EffectType.Apply, 4, target: _source, effectId: _7_0LogParsing.RevivedCombatId);

        var state = CombatLogStateBuilder.CurrentState;
        Assert.Multiple(() =>
        {
            Assert.That(state.EnemyDeathChangeInfo[_target][Start], Is.False);
            Assert.That(state.EnemyDeathChangeInfo[_target][Start.AddSeconds(2)], Is.True);
            Assert.That(state.EnemyDeathChangeInfo[_target], Has.Count.EqualTo(2));
            Assert.That(state.PlayerDeathChangeInfo[_source][Start.AddSeconds(3)], Is.True);
            Assert.That(state.PlayerDeathChangeInfo[_source][Start.AddSeconds(4)], Is.False);
        });
    }

    private CombatModifier[] History() => CombatLogStateBuilder.CurrentState.Modifiers[EffectId].Values.ToArray();

    private void Update(EffectType type, int seconds, Entity source = null, Entity target = null,
        int charges = 0, ulong effectId = EffectId) => CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(new ParsedLogEntry
    {
        TimeStamp = Start.AddSeconds(seconds),
        SourceInfo = new EntityInfo { Entity = source ?? _source },
        TargetInfo = new EntityInfo { Entity = target ?? _target },
        Ability = "Test effect",
        Effect = new Effect { EffectId = effectId, EffectName = "Test effect", EffectType = type },
        Value = new Value { DblValue = charges }
    }, false);
}
