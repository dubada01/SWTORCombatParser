using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CacheLifecycleTests
{
    private static readonly DateTime Start = new(2026, 11, 7, 12, 0, 0);
    private static readonly Type Cache = typeof(Combat).Assembly.GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager", true);
    private static readonly FieldInfo CacheDirectory = Cache.GetField("_sessionCacheDirectory", BindingFlags.Static | BindingFlags.NonPublic);
    private LogState _previousState;
    private object _previousDirectory;
    private string _directory;
    private Entity _player;
    private Entity _enemy;

    [SetUp]
    public void Setup()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.ClearState();
        _previousDirectory = CacheDirectory.GetValue(null);
        _directory = Path.Combine(Path.GetTempPath(), "orbs-cache-lifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        CacheDirectory.SetValue(null, _directory);
        _player = new Entity { Id = 1, LogId = 1, Name = "Player", IsCharacter = true };
        _enemy = new Entity { Id = 2, LogId = 42, Name = "Enemy" };
    }

    [TearDown]
    public void Cleanup()
    {
        CacheDirectory.SetValue(null, _previousDirectory);
        CombatLogStateBuilder.CurrentState = _previousState;
        Directory.Delete(_directory, true);
    }

    [Test]
    public void ReloadedEntitiesRetainBuffAndDeathHistoryFromTheirParsedLog()
    {
        var apply = Entry(0, _player, _player, EffectType.Apply, 1234);
        var remove = Entry(10, _player, _player, EffectType.Remove, 1234);
        CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(apply, false);
        CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(remove, false);
        var combat = Shell(apply, remove);
        var path = (string)Call("CacheCombat", combat);
        var loaded = (List<ParsedLogEntry>)Call("LoadLogs", path);
        var reloadedPlayer = loaded[0].Source;
        var state = CombatLogStateBuilder.CurrentState;
        Assert.Multiple(() =>
        {
            Assert.That(state.GetEffectsWithTarget(Start, Start.AddSeconds(10), reloadedPlayer), Has.Count.EqualTo(1));
            Assert.That(state.GetEffectsWithSource(Start, Start.AddSeconds(10), reloadedPlayer), Has.Count.EqualTo(1));
            Assert.That(state.GetPersonalEffects(Start, Start.AddSeconds(10), reloadedPlayer), Has.Count.EqualTo(1));
            Assert.That(state.WasPlayerDeadAtTime(reloadedPlayer, Start.AddSeconds(5)), Is.False);
        });
    }

    [Test]
    public void BuildingOverallScopeDoesNotReplaceEntitiesInsideALoadedCombat()
    {
        var log = Entry(0, _player, _enemy, EffectType.Apply, 1234);
        CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(log, false);
        var combat = Shell(log);
        var past = new PastCombat();
        past.CacheCombat(combat, keepLoaded: true);
        var overallLogs = past.LoadLogs();
        // This is the normalization performed by EncounterCombat.GetOverallCombat.
        Call("NormalizeLogEntities", overallLogs);
        Assert.Multiple(() =>
        {
            Assert.That(combat.AllLogs[0].Source, Is.SameAs(_player));
            Assert.That(combat.AllLogs[0].Target, Is.SameAs(_enemy));
        });
    }

    [Test]
    public void ReloadedNpcInstanceResolvesItsOwnTargetRatherThanAnotherNpcOfTheSameType()
    {
        var otherEnemy = new Entity { Id = 3, LogId = 42, Name = "Enemy" };
        var otherPlayer = new Entity { Id = 4, LogId = 4, Name = "Other player", IsCharacter = true };
        var first = Entry(0, _enemy, _player, EffectType.TargetChanged, _7_0LogParsing.TargetSetId);
        var second = Entry(1, otherEnemy, otherPlayer, EffectType.TargetChanged, _7_0LogParsing.TargetSetId);
        CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(first, false);
        CombatLogStateBuilder.UpdateCurrentStateWithSingleLog(second, false);
        var path = (string)Call("CacheCombat", Shell(first, second));
        var loaded = (List<ParsedLogEntry>)Call("LoadLogs", path);
        var state = CombatLogStateBuilder.CurrentState;
        var firstTrackedNpc = state.EnemyTargetsInfo.Keys.First();
        var queriedNpc = loaded.Single(log => log.Source.Id != firstTrackedNpc.Id).Source;
        var expected = queriedNpc.Id == _enemy.Id ? _player : otherPlayer;
        var target = state.GetEnemyTargetAtTime(queriedNpc, Start.AddSeconds(2)).Entity;
        Assert.That(target.Id, Is.EqualTo(expected.Id));
    }

    [TestCase("missing")]
    [TestCase("corrupt")]
    [TestCase("truncated")]
    public void UnavailableCacheDoesNotThrowOrEraseTheCombatRowSummary(string failure)
    {
        var past = new PastCombat();
        past.CacheCombat(Shell(Entry(0, _player, _enemy, EffectType.Apply, 1234),
            Entry(10, _player, _enemy, EffectType.Remove, 1234)));
        var path = past.CombatCachePath;
        Assert.That(past.Combat, Is.Null, "Exercise a cache miss, not an already-resident combat.");
        if (failure == "missing") File.Delete(path);
        else if (failure == "corrupt") File.WriteAllText(path, "invalid compressed combat");
        else
        {
            using var stream = File.OpenWrite(path);
            stream.SetLength(stream.Length / 2);
        }
        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => past.GetCombat());
            Assert.That(past.CombatStartTime, Is.EqualTo(Start));
            Assert.That(past.CombatDurationSeconds, Is.EqualTo(10));
            Assert.That(past.Combat, Is.Null);
            Assert.That(past.CombatLoadError, Does.Contain("Reload"));
            Assert.Throws<IOException>(() => past.LoadLogs(), "An overall scope must not silently drop this combat.");
        });
    }

    [Test]
    public void RestoredCacheCanBeRetriedAfterAReadFailure()
    {
        var past = new PastCombat();
        past.CacheCombat(Shell(Entry(0, _player, _enemy, EffectType.Apply, 1234),
            Entry(10, _player, _enemy, EffectType.Remove, 1234)));
        var original = File.ReadAllBytes(past.CombatCachePath);
        File.Delete(past.CombatCachePath);
        Assert.That(past.GetCombat().StartTime, Is.EqualTo(DateTime.MinValue));
        Assert.That(past.Combat, Is.Null);
        File.WriteAllBytes(past.CombatCachePath, original);
        var recovered = past.GetCombat();
        Assert.Multiple(() =>
        {
            Assert.That(recovered.StartTime, Is.EqualTo(Start));
            Assert.That(recovered.AllLogs, Has.Count.EqualTo(2));
            Assert.That(past.Combat, Is.SameAs(recovered));
            Assert.That(past.CombatLoadError, Is.Empty);
            Assert.That(past.CombatDurationSeconds, Is.EqualTo(10));
        });
    }

    [Test]
    public void CacheOwnedByAnotherLogStateReportsReloadInsteadOfUsingWrongScope()
    {
        var owner = CombatLogStateBuilder.CurrentState;
        var past = new PastCombat();
        past.CacheCombat(Shell(Entry(0, _player, _enemy, EffectType.Apply, 1234),
            Entry(10, _player, _enemy, EffectType.Remove, 1234)));
        CombatLogStateBuilder.ClearState();
        Assert.Multiple(() =>
        {
            Assert.That(past.GetCombat().StartTime, Is.EqualTo(DateTime.MinValue));
            Assert.That(past.Combat, Is.Null);
            Assert.That(past.CombatLoadError, Does.Contain("Reload"));
            Assert.That(past.CombatStartTime, Is.EqualTo(Start));
            Assert.Throws<IOException>(() => past.LoadLogs());
        });
        CombatLogStateBuilder.CurrentState = owner;
        Assert.That(past.GetCombat().StartTime, Is.EqualTo(Start));
    }

    [Test]
    public void ReloadedCombatRowRetainsFullDurationWhileAnotherPhaseIsSelected()
    {
        var previousPhases = PhaseManager.SelectedPhases;
        var duration = typeof(PhaseManager).GetField("phaseDuration", BindingFlags.Static | BindingFlags.NonPublic);
        var previousDuration = duration.GetValue(null);
        try
        {
            var past = new PastCombat();
            past.CacheCombat(Shell(Entry(0, _player, _enemy, EffectType.Apply, 1234),
                Entry(10, _player, _enemy, EffectType.Remove, 1234)));
            PhaseManager.SelectedPhases = new List<PhaseInstance>
            {
                new() { PhaseStart = Start.AddSeconds(1), PhaseEnd = Start.AddSeconds(3) }
            };
            PhaseManager.SetSelectedPhaseDuration();
            Assert.That(past.GetCombat().StartTime, Is.EqualTo(Start));
            Assert.That(past.CombatDurationSeconds, Is.EqualTo(10));
            Assert.That(past.CombatDuration, Is.EqualTo("00:10"));
        }
        finally
        {
            PhaseManager.SelectedPhases = previousPhases;
            duration.SetValue(null, previousDuration);
        }
    }

    [Test]
    public void HistoricalRebuildDoesNotResetTheLiveBossNotificationState()
    {
        var previous = EncounterTimerTrigger.CurrentEncounter;
        var notifications = 0;
        void OnBoss(string encounter, string boss, string difficulty) => notifications++;
        EncounterTimerTrigger.BossCombatDetected += OnBoss;
        try
        {
            EncounterTimerTrigger.CurrentEncounter = ("Live instance", "Current boss", "Story");
            EncounterTimerTrigger.FireBossCombatDetected("Historical instance", "Past boss", "Veteran", false);
            EncounterTimerTrigger.FireBossCombatDetected("Live instance", "Current boss", "Story", true);
            Assert.That(notifications, Is.Zero, "Loading an old combat must not retrigger the active boss's timers.");
            EncounterTimerTrigger.FireBossCombatDetected("Live instance", "Next boss", "Story", true);
            Assert.That(notifications, Is.EqualTo(1));
        }
        finally
        {
            EncounterTimerTrigger.BossCombatDetected -= OnBoss;
            EncounterTimerTrigger.CurrentEncounter = previous;
        }
    }

    [Test]
    public void ReturningToARowDuringItsLoadSharesOneCombatWithoutBlockingDeselection()
    {
        using var loaderEntered = new ManualResetEventSlim();
        using var releaseLoader = new ManualResetEventSlim();
        using var secondStarted = new ManualResetEventSlim();
        var loads = 0;
        Combat NewCombat() => Shell(Entry(0, _player, _enemy, EffectType.Apply, 1234),
            Entry(10, _player, _enemy, EffectType.Remove, 1234));
        var loader = (Func<string, Combat>)(_ =>
        {
            Interlocked.Increment(ref loads);
            loaderEntered.Set();
            if (!releaseLoader.Wait(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("The test did not release the cache load.");
            return NewCombat();
        });
        var constructor = typeof(PastCombat).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(Func<string, Combat>) }, null);
        var past = (PastCombat)constructor.Invoke(new object[] { loader });
        past.CacheCombat(NewCombat());
        past.IsSelected = true;
        var results = new Combat[2];
        var errors = new Exception[2];
        var first = new Thread(() =>
        {
            try { results[0] = past.GetCombat(); }
            catch (Exception error) { errors[0] = error; }
        }) { IsBackground = true };
        var second = new Thread(() =>
        {
            secondStarted.Set();
            try { results[1] = past.GetCombat(); }
            catch (Exception error) { errors[1] = error; }
        }) { IsBackground = true };

        first.Start();
        try
        {
            Assert.That(loaderEntered.Wait(TimeSpan.FromSeconds(5)), Is.True);
            // A -> B: UI deselection must finish while the first A read is blocked.
            var unload = Task.Run(() =>
            {
                past.IsSelected = false;
                past.UnloadCombat();
            });
            Assert.That(unload.Wait(TimeSpan.FromSeconds(5)), Is.True,
                "Unloading must not wait for the cache reader's lock.");
            Assert.That(past.Combat, Is.Null);

            // B -> A: a second worker reaches either the load gate or the injected loader.
            // Waiting for its blocked state makes this independent of thread scheduling.
            past.IsSelected = true;
            second.Start();
            Assert.That(secondStarted.Wait(TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(SpinWait.SpinUntil(() =>
                (second.ThreadState & ThreadState.WaitSleepJoin) != 0 || !second.IsAlive,
                TimeSpan.FromSeconds(5)), Is.True);
            Assert.That(Volatile.Read(ref loads), Is.EqualTo(1),
                "Two readers of the selected row must share the same rebuild.");
        }
        finally
        {
            releaseLoader.Set();
            first.Join(TimeSpan.FromSeconds(5));
            if ((second.ThreadState & ThreadState.Unstarted) == 0)
                second.Join(TimeSpan.FromSeconds(5));
        }

        Assert.Multiple(() =>
        {
            Assert.That(errors, Is.All.Null);
            Assert.That(results[0], Is.Not.Null);
            Assert.That(results[1], Is.SameAs(results[0]));
            Assert.That(past.Combat, Is.SameAs(results[1]));
            Assert.That(past.GetCombat(), Is.SameAs(results[1]));
            Assert.That(loads, Is.EqualTo(1));
        });

        past.IsSelected = false;
        past.UnloadCombat();
        Assert.That(past.Combat, Is.Null);
        past.IsSelected = true;
        var reselected = past.GetCombat();
        Assert.That(reselected, Is.Not.SameAs(results[0]));
        Assert.That(reselected, Is.SameAs(past.Combat));
        Assert.That(loads, Is.EqualTo(2), "A later selection can reload after the row is released.");
    }

    private static object Call(string method, params object[] args) => Cache.GetMethod(method,
        BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

    private static Combat Shell(params ParsedLogEntry[] logs)
    {
        var combat = new Combat { StartTime = logs.Min(log => log.TimeStamp), EndTime = logs.Max(log => log.TimeStamp), BossInfo = new BossInfo() };
        foreach (var log in logs) combat.AllLogs[log.LogLineNumber] = log;
        return combat;
    }

    private static ParsedLogEntry Entry(int seconds, Entity source, Entity target, EffectType type, ulong effectId) => new()
    {
        LogLineNumber = seconds, TimeStamp = Start.AddSeconds(seconds), LogName = "fixture.txt",
        SourceInfo = new EntityInfo { Entity = source }, TargetInfo = new EntityInfo { Entity = target },
        Ability = "Fixture effect", AbilityId = 1234,
        Effect = new Effect { EffectType = type, EffectId = effectId, EffectName = "Fixture effect" },
        Value = new Value { DblValue = 1 }
    };
}
