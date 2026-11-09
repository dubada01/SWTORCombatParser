using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Challenges;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class ChallengeReplayTests
{
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags Static = BindingFlags.NonPublic | BindingFlags.Static;
    private Combat _previousCombat;

    [SetUp]
    public void SaveCurrentCombat() => _previousCombat = CombatIdentifier.CurrentCombat;

    [TearDown]
    public void RestoreCurrentCombat() => CombatIdentifier.CurrentCombat = _previousCombat;

    [Test]
    public void ReplayMatchesLiveReferenceAcrossChallengeTypesAndPhaseOrCondition()
    {
        var phaseId = Guid.NewGuid();
        var challenges = new[]
        {
            Definition(ChallengeType.DamageOut, "42", target: "Target"),
            Definition(ChallengeType.DamageOut, "Hit", target: "2"),
            Definition(ChallengeType.DamageIn, "Hit", source: "Source"),
            Definition(ChallengeType.DamageIn, "42", source: "1"),
            Definition(ChallengeType.DamageOut, null),
            Definition(ChallengeType.AbilityCount, "42"),
            Definition(ChallengeType.InterruptCount),
            Definition(ChallengeType.EffectStacks, "Effect"),
            Definition(ChallengeType.EffectStacks, "99"),
            Definition(ChallengeType.DamageOut, "Hit", boss: "Other"),
            Definition(ChallengeType.DamageIn, "Missing"),
            Definition(ChallengeType.MetricDuringPhase, boss: "Other", phase: phaseId),
            Definition(ChallengeType.MetricDuringPhase, phase: Guid.NewGuid())
        };
        var logs = new[] { Line(42, "Hit"), Line(_7_0LogParsing.InterruptCombatId, "Interrupt"), Line(42, "Hit") };
        var phases = new[] { new PhaseInstance { SourcePhase = new Phase { Id = phaseId } } };
        var definitions = new ConcurrentDictionary<Guid, Challenge>(challenges.Select(challenge =>
            new KeyValuePair<Guid, Challenge>(challenge.Id, challenge)));
        var updater = EmptyUpdater();
        Set(updater, "_allChallenges", definitions);
        Set(updater, "_currentBossName", "Boss");
        var phaseIds = phases.Select(phase => phase.SourcePhase.Id).ToHashSet();
        var reference = logs.SelectMany(log => RecordLive(updater, log, phaseIds)).ToArray();
        var actual = Scan(logs, definitions.Values, phases);
        Assert.That(actual.Select(challenge => challenge.Id), Is.EquivalentTo(reference.Select(challenge => challenge.Id)));
        Assert.That(actual.Count, Is.EqualTo(10));
        Assert.That(actual, Does.Contain(challenges[11]), "Phase matching is an OR condition even for another boss.");
    }

    [Test]
    public void ReplayKeepsFirstActivationChronologyWithoutRecheckingActivatedDefinitions()
    {
        var later = Definition(ChallengeType.AbilityCount, "2");
        var first = Definition(ChallengeType.AbilityCount, "1");
        var enumerated = 0;
        IEnumerable<ParsedLogEntry> Logs()
        {
            foreach (var ability in new ulong[] { 1, 1, 2, 1 })
            {
                enumerated++;
                yield return Line(ability, "Ability");
            }
        }

        var actual = Scan(Logs(), new[] { later, first });
        Assert.That(actual, Is.EqualTo(new[] { first, later }));
        Assert.That(enumerated, Is.EqualTo(3), "Once every candidate is active, the remaining log is unnecessary.");
    }

    [Test]
    public void ReplayWithoutCandidatesDoesNotEnumerateTheLog()
    {
        IEnumerable<ParsedLogEntry> Logs()
        {
            Assert.Fail("A different boss with no matching phases must not scan log entries.");
            yield break;
        }
        Assert.That(Scan(Logs(), new[] { Definition(ChallengeType.DamageOut, boss: "Other") }), Is.Empty);
    }

    [Test]
    public void EmptyCombatDoesNotActivatePhaseChallenges()
    {
        var phaseId = Guid.NewGuid();
        Assert.That(Scan(Array.Empty<ParsedLogEntry>(),
            new[] { Definition(ChallengeType.MetricDuringPhase, phase: phaseId) },
            new[] { new PhaseInstance { SourcePhase = new Phase { Id = phaseId } } }), Is.Empty);
    }

    [Test]
    public void ObsoleteReplayDiscardsPartialScan()
    {
        var current = true;
        IEnumerable<ParsedLogEntry> Logs()
        {
            yield return Line(1, "First");
            current = false;
            yield return Line(2, "Second");
            Assert.Fail("A superseded replay must stop scanning.");
        }
        Assert.That(Scan(Logs(), new[] { Definition(ChallengeType.AbilityCount, "1"),
            Definition(ChallengeType.AbilityCount, "2") }, isCurrent: () => current), Is.Null);
    }

    [Test]
    public void ObsoletePublicationLeavesCurrentChallengeStateUntouched()
    {
        var updater = EmptyUpdater();
        var current = Definition(ChallengeType.DamageOut);
        var active = new List<Challenge> { current };
        var viewModels = new ObservableCollection<ChallengeInstanceViewModel>();
        Set(updater, "_activeChallenges", active);
        Set(updater, "_challenges", viewModels);
        Set(updater, "_currentBossName", "Current boss");
        Set(updater, "_replayGeneration", 2);
        var published = (bool)typeof(ChallengeUpdater).GetMethod("PublishReplay", Instance)!
            .Invoke(updater, new object[] { new Combat(), new List<Challenge>(), Array.Empty<PhaseInstance>(), 1 })!;
        Assert.That(published, Is.False);
        Assert.That(active, Is.EqualTo(new[] { current }));
        Assert.That(typeof(ChallengeUpdater).GetField("_currentBossName", Instance)!.GetValue(updater), Is.EqualTo("Current boss"));
    }

    [Test]
    public void LiveActivationAfterReplaySnapshotSurvivesCurrentPublication()
    {
        var first = Definition(ChallengeType.AbilityCount, "1");
        var oneShot = Definition(ChallengeType.AbilityCount, "2");
        var updater = LiveUpdater(first, oneShot);
        var combat = CurrentCombat();
        TrackStart(updater, combat.StartTime);
        var generation = BeginReplay(updater, combat);
        var scanned = Scan(new[] { Line(1, "First") }, new[] { first, oneShot });

        // The live line fires before the streamer's pending batch reaches
        // Combat.AllLogs, and after the replay worker captured its older logs.
        Assert.That(RecordLive(updater, Line(2, "One-shot")), Is.EqualTo(new[] { oneShot }));
        RecordLive(updater, Line(1, "First"));
        var published = MergeReplay(updater, combat, scanned, generation);

        Assert.That(published, Is.EquivalentTo(new[] { first, oneShot }));
        Assert.That(published.Count(challenge => challenge.Id == first.Id), Is.EqualTo(1));
    }

    [Test]
    public void HistoricalPublicationPreservesLiveActivationsForReturningToCurrentCombat()
    {
        var oneShot = Definition(ChallengeType.AbilityCount, "2");
        var updater = LiveUpdater(oneShot);
        var current = CurrentCombat();
        TrackStart(updater, current.StartTime);
        var historical = new Combat { StartTime = current.StartTime.AddMinutes(-10),
            EncounterBossDifficultyParts = ("Different historical boss", "", "") };
        var historicalGeneration = BeginReplay(updater, historical);

        RecordLive(updater, Line(2, "One-shot"));
        var historicalPublished = (bool)typeof(ChallengeUpdater).GetMethod("PublishReplay", Instance)!
            .Invoke(updater, new object[] { historical, new List<Challenge>(), Array.Empty<PhaseInstance>(), historicalGeneration })!;

        Assert.Multiple(() =>
        {
            Assert.That(historicalPublished, Is.True);
            Assert.That(Get<List<Challenge>>(updater, "_activeChallenges"), Is.Empty,
                "A historical result must not inherit the live fight's one-shot activation.");
            Assert.That(Get<string>(updater, "_currentBossName"), Is.EqualTo("Boss"),
                "Historical publication must not replace the live boss used for subsequent raw events.");
        });
        var currentGeneration = BeginReplay(updater, current);
        Assert.That(MergeReplay(updater, current, new List<Challenge>(), currentGeneration), Is.EqualTo(new[] { oneShot }));
    }

    [Test]
    public void ResetInvalidatesOldRunEvenIfAnotherLogReusesItsStartTime()
    {
        var oneShot = Definition(ChallengeType.AbilityCount, "2");
        var updater = LiveUpdater(oneShot);
        var combat = CurrentCombat();
        TrackStart(updater, combat.StartTime);
        var oldGeneration = BeginReplay(updater, combat);
        RecordLive(updater, Line(2, "One-shot"));

        typeof(ChallengeUpdater).GetMethod("ResetLiveChallengeState", Instance)!.Invoke(updater, null);
        var nextCombat = CurrentCombat(); // Same boss/time in another file must still be a new epoch.
        TrackStart(updater, nextCombat.StartTime);

        Assert.That(MergeReplay(updater, combat, new List<Challenge> { oneShot }, oldGeneration), Is.Null);
        var newGeneration = BeginReplay(updater, nextCombat);
        Assert.That(MergeReplay(updater, nextCombat, new List<Challenge>(), newGeneration), Is.Empty);
        Assert.That(RecordLive(updater, Line(2, "One-shot")), Is.EqualTo(new[] { oneShot }),
            "A new fight must be able to activate the same challenge again.");
    }

    [Test]
    public void FirstLiveUpdateAfterHistoricalHandoffKeepsAlreadyObservedActivations()
    {
        var oneShot = Definition(ChallengeType.AbilityCount, "2");
        var updater = LiveUpdater(oneShot);
        var combat = CurrentCombat();
        var generation = BeginReplay(updater, combat);
        RecordLive(updater, Line(2, "One-shot"));
        typeof(ChallengeUpdater).GetMethod("TrackLiveCombat", Instance)!.Invoke(updater,
            new object[] { new CombatStatusUpdate { Type = UpdateType.Update, CombatStartTime = combat.StartTime } });
        Assert.That(MergeReplay(updater, combat, new List<Challenge>(), generation), Is.EqualTo(new[] { oneShot }));
    }

    [Test]
    public void ScopedCopyWithTheLiveStartTimeDoesNotInheritOutOfScopeActivations()
    {
        var oneShot = Definition(ChallengeType.AbilityCount, "2");
        var updater = LiveUpdater(oneShot);
        var current = CurrentCombat();
        TrackStart(updater, current.StartTime);
        RecordLive(updater, Line(2, "One-shot"));
        var scoped = new Combat { StartTime = current.StartTime, EncounterBossDifficultyParts = ("Boss", "", "") };
        var scopedGeneration = BeginReplay(updater, scoped);
        typeof(ChallengeUpdater).GetMethod("TrackLiveCombat", Instance)!.Invoke(updater,
            new object[] { new CombatStatusUpdate { Type = UpdateType.Update, CombatStartTime = current.StartTime } });

        Assert.That(Get<bool>(updater, "_showingLiveChallenges"), Is.False);
        Assert.That(MergeReplay(updater, scoped, new List<Challenge>(), scopedGeneration), Is.Empty);
        var currentGeneration = BeginReplay(updater, current);
        Assert.That(MergeReplay(updater, current, new List<Challenge>(), currentGeneration), Is.EqualTo(new[] { oneShot }));
    }

    private static Combat CurrentCombat()
    {
        var combat = new Combat
        {
            StartTime = new DateTime(2025, 6, 27, 20, 0, 0), EncounterBossDifficultyParts = ("Boss", "", "")
        };
        CombatIdentifier.CurrentCombat = combat;
        return combat;
    }

    private static ChallengeUpdater LiveUpdater(params Challenge[] definitions)
    {
        var updater = EmptyUpdater();
        Set(updater, "_allChallenges", new ConcurrentDictionary<Guid, Challenge>(definitions.Select(challenge =>
            new KeyValuePair<Guid, Challenge>(challenge.Id, challenge))));
        Set(updater, "_currentBossName", "Boss");
        return updater;
    }

    private static void TrackStart(ChallengeUpdater updater, DateTime start) =>
        typeof(ChallengeUpdater).GetMethod("TrackLiveCombat", Instance)!.Invoke(updater,
            new object[] { new CombatStatusUpdate { Type = UpdateType.Start, CombatStartTime = start } });

    private static int BeginReplay(ChallengeUpdater updater, Combat combat) =>
        (int)typeof(ChallengeUpdater).GetMethod("BeginReplay", Instance)!.Invoke(updater, new object[] { combat })!;

    private static List<Challenge> MergeReplay(ChallengeUpdater updater, Combat combat, List<Challenge> activated, int generation) =>
        (List<Challenge>)typeof(ChallengeUpdater).GetMethod("MergeReplayActivations", Instance)!
            .Invoke(updater, new object[] { combat, activated, generation });

    private static IReadOnlyList<Challenge> RecordLive(ChallengeUpdater updater, ParsedLogEntry log,
        IReadOnlySet<Guid> phaseIds = null) =>
        (IReadOnlyList<Challenge>)typeof(ChallengeUpdater).GetMethod("FindNewChallengesFromLog", Instance)!
            .Invoke(updater, new object[] { log, phaseIds ?? new HashSet<Guid>() })!;

    private static List<Challenge> Scan(IEnumerable<ParsedLogEntry> logs, IEnumerable<Challenge> definitions,
        IEnumerable<PhaseInstance> phases = null, Func<bool> isCurrent = null) =>
        (List<Challenge>)typeof(ChallengeUpdater).GetMethod("FindReplayChallenges", Static)!.Invoke(null,
            new object[] { logs, definitions, "Boss", phases ?? Array.Empty<PhaseInstance>(), isCurrent ?? (() => true) });

    private static ChallengeUpdater EmptyUpdater()
    {
        var updater = (ChallengeUpdater)RuntimeHelpers.GetUninitializedObject(typeof(ChallengeUpdater));
        Set(updater, "_activeChallenges", new List<Challenge>());
        Set(updater, "_challenges", new ObservableCollection<ChallengeInstanceViewModel>());
        Set(updater, "_challengeStateLock", new object());
        Set(updater, "_liveActiveChallenges", new Dictionary<Guid, Challenge>());
        Set(updater, "_displayedUsesLiveShell", true);
        Set(updater, "_showingLiveChallenges", true);
        return updater;
    }

    private static T Get<T>(ChallengeUpdater updater, string name) =>
        (T)typeof(ChallengeUpdater).GetField(name, Instance)!.GetValue(updater)!;

    private static void Set(ChallengeUpdater updater, string name, object value) =>
        typeof(ChallengeUpdater).GetField(name, Instance)!.SetValue(updater, value);

    private static Challenge Definition(ChallengeType type, string value = null, string source = null,
        string target = null, string boss = "Boss", Guid phase = default) => new()
    {
        Id = Guid.NewGuid(), Source = "Encounter|" + boss, ChallengeType = type,
        Value = value, ChallengeSource = source, ChallengeTarget = target, PhaseId = phase
    };

    private static ParsedLogEntry Line(ulong ability, string name) => new()
    {
        AbilityId = ability, Ability = name,
        SourceInfo = new EntityInfo { Entity = new Entity { Name = "Source", LogId = 1 } },
        TargetInfo = new EntityInfo { Entity = new Entity { Name = "Target", LogId = 2 } },
        Effect = new Effect { EffectName = "Effect", EffectId = 99 }
    };
}
