using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.AbilityInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class ShieldingProcessorTests
{
    private static readonly DateTime Start = new(2026, 11, 7, 12, 0, 0);
    private static readonly MethodInfo Process = typeof(CombatIdentifier).Assembly
        .GetType("SWTORCombatParser.Model.CombatParsing.ShieldingProcessor", true)
        .GetMethod("AddShieldLogsByTarget", BindingFlags.Public | BindingFlags.Static);
    private LogState _previousState;
    private Dictionary<double, AbilityInfo> _previousAbilities;
    private Entity _source;
    private Entity _secondSource;
    private Entity _target;

    [SetUp]
    public void SetUp()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.ClearState();
        _previousAbilities = AbilityLoader.AbsorbAbilities;
        AbilityLoader.AbsorbAbilities = new Dictionary<double, AbilityInfo> { [1] = new() { name = "Shield" } };
        _source = new Entity { Name = "First healer", Id = 1, LogId = 1, IsCharacter = true };
        _secondSource = new Entity { Name = "Second healer", Id = 2, LogId = 2, IsCharacter = true };
        _target = new Entity { Name = "Tank", Id = 3, LogId = 3, IsCharacter = true };
    }

    [TearDown]
    public void TearDown()
    {
        CombatLogStateBuilder.CurrentState = _previousState;
        AbilityLoader.AbsorbAbilities = _previousAbilities;
    }

    [Test]
    public void PartialAbsorbConsumesAShieldOnlyForTheCurrentCalculation()
    {
        var modifier = AddShield(_source, 0, 5);
        var hits = new[] { Hit(1, 100, 30, 70), Hit(2, 100, 40, 60) };
        var first = Build(hits);
        var repeated = Build(hits);

        Assert.Multiple(() =>
        {
            Assert.That(first.TotalProvidedSheilding[_source], Is.EqualTo(30));
            Assert.That(repeated.TotalProvidedSheilding[_source], Is.EqualTo(30));
            Assert.That(first.ShieldingProvidedLogs[_source].Single().TimeStamp, Is.EqualTo(Start.AddSeconds(5)));
            Assert.That(modifier.HasAbsorbBeenCounted, Is.False);
        });
    }

    [Test]
    public void FullyAbsorbedHitsAccumulateUntilAPartialAbsorbConsumesTheShield()
    {
        AddShield(_source, 0, 5);
        var combat = Build(new[]
        {
            Hit(1, 100, 100, 0), Hit(2, 100, 100, 0), Hit(3, 100, 30, 70), Hit(4, 100, 40, 60)
        });

        Assert.That(combat.TotalProvidedSheilding[_source], Is.EqualTo(230));
        Assert.That(combat.ShieldingProvidedLogs[_source], Has.Count.EqualTo(1));
    }

    [Test]
    public void OverlappingShieldsKeepTheExistingSplitAndConsumptionRules()
    {
        AddShield(_source, 0, 5);
        AddShield(_secondSource, 0.5, 5);
        var combat = Build(new[] { Hit(1, 100, 30, 50), Hit(2, 100, 40, 60), Hit(3, 100, 50, 50) });

        Assert.Multiple(() =>
        {
            Assert.That(combat.TotalProvidedSheilding[_source], Is.EqualTo(20));
            Assert.That(combat.TotalProvidedSheilding[_secondSource], Is.EqualTo(70));
        });
    }

    [Test]
    public void EffectStartIsExclusiveAndTheFourPointTwoFiveSecondGraceIsInclusive()
    {
        AddShield(_source, 0, 5);
        var combat = Build(new[]
        {
            Hit(0, 100, 100, 0), Hit(1, 100, 100, 0), Hit(9.25, 100, 100, 0), Hit(9.251, 100, 100, 0)
        });

        Assert.That(combat.TotalProvidedSheilding[_source], Is.EqualTo(200));
    }

    [Test]
    public void ParallelCalculationsDoNotReadOrResetAnotherPassesConsumptionFlags()
    {
        var modifier = AddShield(_source, 0, 5);
        // Represents another calculation paused after marking the shared modifier.
        // This makes the isolation check deterministic instead of depending on a race.
        modifier.HasAbsorbBeenCounted = true;
        var unrelated = AddShield(_secondSource, -100, -95);
        unrelated.HasAbsorbBeenCounted = true;
        var results = new Combat[16];
        Parallel.For(0, results.Length, i => results[i] = Build(new[] { Hit(1, 100, 30, 70), Hit(2, 100, 40, 60) }));

        Assert.Multiple(() =>
        {
            Assert.That(results.Select(combat => combat.TotalProvidedSheilding.GetValueOrDefault(_source)),
                Is.All.EqualTo(30));
            Assert.That(modifier.HasAbsorbBeenCounted, Is.True, "A calculation must not reset another pass's state.");
            Assert.That(unrelated.HasAbsorbBeenCounted, Is.True, "Unrelated historical modifiers must remain untouched.");
        });
    }

    [Test]
    public void RebuildingTheSameCombatReplacesSyntheticRecordsWithoutGrowingRawHistory()
    {
        AddShield(_source, 0, 5);
        var hits = new[] { Hit(1, 100, 30, 70), Hit(2, 100, 40, 60) };
        var combat = Build(hits);
        for (var pass = 0; pass < 5; pass++)
        {
            Process.Invoke(null, new object[] { new Dictionary<Entity, List<ParsedLogEntry>> { [_target] = hits.ToList() }, combat });
            Assert.That(combat.AllLogs, Has.Count.EqualTo(3));
            Assert.That(combat.AllLogs[0], Is.SameAs(hits[0]));
            Assert.That(combat.AllLogs[1], Is.SameAs(hits[1]));
            Assert.That(combat.TotalProvidedSheilding[_source], Is.EqualTo(30));
            Assert.That(combat.AllLogs.Values.Count(log => log.Effect.EffectType == EffectType.AbsorbShield), Is.EqualTo(1));
        }
    }

    [Test]
    public void PhysicalRecordAtAPreviousSyntheticKeySurvivesTheNextRebuild()
    {
        AddShield(_source, 0, 5);
        var first = Hit(1, 100, 30, 70);
        var combat = Build(new[] { first });
        var syntheticKey = combat.AllLogs.Single(pair => pair.Value.Effect.EffectType == EffectType.AbsorbShield).Key;
        var appended = Hit(2, 100, 40, 60);
        appended.LogLineNumber = syntheticKey;
        combat.AllLogs[syntheticKey] = appended;
        Process.Invoke(null, new object[] { new Dictionary<Entity, List<ParsedLogEntry>> { [_target] = new() { first, appended } }, combat });
        Assert.That(combat.AllLogs[syntheticKey], Is.SameAs(appended));
        Assert.That(combat.AllLogs, Has.Count.EqualTo(3));
        Assert.That(combat.AllLogs.Single(pair => pair.Value.Effect.EffectType == EffectType.AbsorbShield).Key, Is.Not.EqualTo(syntheticKey));
    }

    [Test]
    public void IncrementalShieldRebuildMatchesFullCombatAndPreservesRawMetrics()
    {
        AddShield(_source, 0, 5);
        var logs = new[] { Hit(1, 100, 100, 0), Hit(2, 100, 30, 70), Hit(6, 100, 40, 60) };
        for (var index = 0; index < logs.Length; index++)
        {
            logs[index].LogLineNumber = index;
            logs[index].Value.Modifier.ValueType = DamageType.absorbed;
        }
        var full = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var incremental = new Combat();
        foreach (var log in logs) CombatIdentifier.GenerateCombatFromLogs(new[] { log }, combatShell: incremental);
        Assert.That(incremental.TotalDamage[_source], Is.EqualTo(full.TotalDamage[_source]));
        Assert.That(incremental.TotalDamageTaken[_target], Is.EqualTo(full.TotalDamageTaken[_target]));
        Assert.That(incremental.TotalProvidedSheilding[_source], Is.EqualTo(130));
        Assert.That(incremental.TotalProvidedSheilding[_source], Is.EqualTo(full.TotalProvidedSheilding[_source]));
        Assert.That(incremental.AllLogs.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Effect.EffectType, pair.Value.Value.EffectiveDblValue)),
            Is.EqualTo(full.AllLogs.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Effect.EffectType, pair.Value.Value.EffectiveDblValue))));
        Assert.That(logs.All(log => ReferenceEquals(incremental.AllLogs[log.LogLineNumber], log)), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ARemovalAfterCombatCannotRetroactivelyChangeShieldAmountsOrCaster(bool fullyAbsorbed)
    {
        // The live raid trace had an earlier open Force Armor competing with a
        // Trauma Shield that closed within the fight. Reading the later removal
        // used to change the old fight's split and could discard its shield total.
        var futureClosed = AddShield(_secondSource, fullyAbsorbed ? 0 : 0.5, 30);
        futureClosed.StopTime = DateTime.MinValue;
        futureClosed.Complete = false;
        AddShield(_source, fullyAbsorbed ? 0.5 : 0, 5);
        var hits = fullyAbsorbed
            ? new[] { Hit(1, 2162, 2162, 0), Hit(2, 2011, 2011, 0) }
            : new[] { Hit(1, 100, 30, 50) };
        var live = Build(hits);
        var expected = fullyAbsorbed ? 4173 : 30;
        Assert.That(live.TotalProvidedSheilding[_source], Is.EqualTo(expected));

        futureClosed.StopTime = Start.AddSeconds(30);
        futureClosed.Complete = true;
        var historical = Build(hits);
        Process.Invoke(null, new object[] { new Dictionary<Entity, List<ParsedLogEntry>> { [_target] = hits.ToList() }, live });
        Assert.Multiple(() =>
        {
            Assert.That(historical.TotalProvidedSheilding.GetValueOrDefault(_source), Is.EqualTo(expected));
            Assert.That(live.TotalProvidedSheilding.GetValueOrDefault(_source), Is.EqualTo(expected));
            Assert.That(historical.TotalProvidedSheilding.GetValueOrDefault(_secondSource), Is.Zero);
            Assert.That(live.AllLogs.Count, Is.EqualTo(historical.AllLogs.Count));
            Assert.That(futureClosed.StopTime, Is.EqualTo(Start.AddSeconds(30)), "Scope queries must not mutate shared modifier history.");
        });
    }

    private CombatModifier AddShield(Entity source, double start, double stop)
    {
        var modifier = new CombatModifier
        {
            Name = "Shield", EffectName = "Shield", EffectId = 1, Source = source, Target = _target,
            StartTime = Start.AddSeconds(start), StopTime = Start.AddSeconds(stop), Complete = true
        };
        var history = CombatLogStateBuilder.CurrentState.Modifiers.GetOrAdd(1, _ => new ConcurrentDictionary<Guid, CombatModifier>());
        history[Guid.NewGuid()] = modifier;
        return modifier;
    }

    private ParsedLogEntry Hit(double seconds, double raw, double absorbed, double mitigated) => new()
    {
        TimeStamp = Start.AddSeconds(seconds),
        SourceInfo = new EntityInfo { Entity = _source },
        TargetInfo = new EntityInfo { Entity = _target },
        Ability = "Attack",
        Effect = new Effect { EffectType = EffectType.Apply, EffectId = _7_0LogParsing._damageEffectId },
        Value = new Value { DblValue = raw, MitigatedDblValue = mitigated, Modifier = new Value { DblValue = absorbed } }
    };

    private Combat Build(IReadOnlyList<ParsedLogEntry> hits)
    {
        var combat = new Combat { StartTime = Start, EndTime = Start.AddSeconds(20) };
        for (var i = 0; i < hits.Count; i++) combat.AllLogs[i] = hits[i];
        // The existing emission rule needs a source record after the shield's stop.
        foreach (var source in new[] { _source, _secondSource })
            combat.LogsInvolvingEntity[source.LogId] = new ConcurrentQueue<ParsedLogEntry>(new[]
            {
                new ParsedLogEntry { TimeStamp = Start.AddSeconds(20), Effect = new Effect() }
            });
        var input = new Dictionary<Entity, List<ParsedLogEntry>> { [_target] = hits.ToList() };
        Process.Invoke(null, new object[] { input, combat });
        return combat;
    }
}
