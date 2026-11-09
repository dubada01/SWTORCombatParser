using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Timers;
using Timer = System.Timers.Timer;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatDetectorTests
{
    private static readonly DateTime Start = new(2025, 1, 1, 12, 0, 0);
    private LogState _previousState;
    private CombatDetector _detector;
    private EncounterInfo _encounter;
    private Entity _player;

    [SetUp]
    public void SetUp()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        _encounter = new EncounterInfo { Name = "Test encounter" };
        var state = new LogState();
        state.EncounterEnteredInfo[Start.AddSeconds(-1)] = _encounter;
        typeof(LogState).GetMethod("CacheEncounterEnterList", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(state, null);
        CombatLogStateBuilder.CurrentState = state;
        _detector = new CombatDetector();
        _player = new Entity { Id = 1, LogId = 1, Name = "Player", IsCharacter = true, IsLocalPlayer = true };
    }

    [TearDown]
    public void TearDown()
    {
        _detector.Reset();
        CombatLogStateBuilder.CurrentState = _previousState;
    }

    [Test]
    public void BossClassificationKeepsDefinitionOrderAndIncludesDamageSources()
    {
        _encounter.BossInfos = new List<BossInfo>
        {
            Boss(new long[] { 10 }, new long[] { 99 }),
            Boss(new long[] { 10, 11 }, new long[] { 11 })
        };
        Assert.That(Check(0, _7_0LogParsing.EnterCombatId), Is.EqualTo(CombatState.EnteredCombat));
        Check(1, _7_0LogParsing._damageEffectId, target: Npc(10));
        // Only the first definition matches initially; killing 11 cannot end it.
        Assert.That(Check(2, _7_0LogParsing.DeathCombatId, target: Npc(11)), Is.EqualTo(CombatState.InCombat));
        Check(3, _7_0LogParsing._damageEffectId, source: Npc(11));
        Assert.That(Check(4, _7_0LogParsing.DeathCombatId, target: Npc(11)), Is.EqualTo(CombatState.ExitedCombat));
    }

    [Test]
    public void BossDefinitionChangesWithinTheSameEncounterRemainVisible()
    {
        _encounter.BossInfos.Add(Boss(new long[] { 10 }, new long[] { 10 }));
        Check(0, _7_0LogParsing.EnterCombatId);
        Check(1, _7_0LogParsing._damageEffectId, target: Npc(10));
        _encounter.BossInfos[0].TargetsRequiredForKill = new List<long> { 11 };
        _encounter.BossInfos[0].TargetIds[0] = 12;
        _encounter.BossInfos.Add(Boss(new long[] { 10 }, new long[] { 13 }));
        Check(2, 0);
        Assert.That(Check(3, _7_0LogParsing.DeathCombatId, target: Npc(11)), Is.EqualTo(CombatState.InCombat));
        Assert.That(Check(4, _7_0LogParsing.DeathCombatId, target: Npc(13)), Is.EqualTo(CombatState.ExitedCombat));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NullAndEmptyDefinitionsPreserveTheirExistingTimeoutBehavior(bool nullDefinitions)
    {
        _encounter.BossInfos.Add(Boss(new long[] { 10 }, new long[] { 10 }));
        Check(0, _7_0LogParsing.EnterCombatId);
        Check(1, _7_0LogParsing._damageEffectId, target: Npc(10));
        _encounter.BossInfos = nullDefinitions ? null : new List<BossInfo>();
        Check(2, _7_0LogParsing.ExitCombatId);
        Assert.That(Check(2.6, 0), Is.EqualTo(nullDefinitions ? CombatState.OutOfCombat : CombatState.InCombat));
        Assert.That(Check(5, 0), Is.EqualTo(CombatState.OutOfCombat));
    }

    [Test]
    public void TrainingDummyUsesBossTimeoutUntilTheNextCombat()
    {
        Check(0, _7_0LogParsing.EnterCombatId);
        Check(1, _7_0LogParsing._damageEffectId, target: Npc(2857785339412480));
        Check(2, _7_0LogParsing.ExitCombatId);
        Assert.That(Check(2.6, 0), Is.EqualTo(CombatState.InCombat));
        Assert.That(Check(5, 0), Is.EqualTo(CombatState.OutOfCombat));
        Check(6, _7_0LogParsing.EnterCombatId);
        Check(7, _7_0LogParsing.ExitCombatId);
        Assert.That(Check(7.6, 0), Is.EqualTo(CombatState.OutOfCombat));
    }

    [Test]
    public void ReviveMatchingUsesTheExactEntityRatherThanSharedLogId()
    {
        _encounter.BossInfos.Add(Boss(new long[] { 10 }, new long[] { 10 }));
        Check(0, _7_0LogParsing.EnterCombatId);
        Check(1, _7_0LogParsing._damageEffectId, target: Npc(10));
        Check(2, 0, ability: 3502674678906880);
        Assert.That(Check(3, _7_0LogParsing.RevivedCombatId), Is.EqualTo(CombatState.InCombat));
        Check(4, 0, ability: 3502674678906880);
        var differentPlayer = new Entity { Id = 2, LogId = _player.LogId, Name = "Different instance", IsCharacter = true };
        Assert.That(Check(5, _7_0LogParsing.RevivedCombatId, source: differentPlayer), Is.EqualTo(CombatState.ExitedCombat));
    }

    [Test]
    public void RepeatedRealtimeExitsPublishOneTimeoutPerCombat()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var arm = typeof(CombatDetector).GetMethod("ExitCombatDetected", flags)!;
        var timer = (Timer)typeof(CombatDetector).GetField("_timeoutTimer", flags)!.GetValue(_detector)!;
        var handlers = typeof(Timer).GetFields(flags).Single(field => field.FieldType == typeof(ElapsedEventHandler));
        var notifications = new List<(CombatState State, bool RealTime)>();
        _detector.AlertExitCombatTimedOut += (state, realTime) => notifications.Add((state, realTime));
        for (var combat = 0; combat < 5; combat++)
        {
            Check(combat * 10, _7_0LogParsing.EnterCombatId);
            // A long interval plus directly raising the registered event makes
            // handler accumulation deterministic, without timing or sleeps.
            arm.Invoke(_detector, new object[] { Log(combat * 10 + 1, _7_0LogParsing.ExitCombatId), true, 3600d });
            timer.Stop();
            ((ElapsedEventHandler)handlers.GetValue(timer)!)(timer, null);
            Assert.That(notifications, Has.Count.EqualTo(combat + 1));
            Assert.That(notifications[^1], Is.EqualTo((CombatState.ExitCombatDelayTimedOut, true)));
            Assert.That(_detector.InCombat, Is.False);
        }
    }

    [TestCase(false, 1)]
    [TestCase(false, 30)]
    [TestCase(true, 30)]
    public void ReplayBatchTimeoutsMatchHistoricalRecordBoundaries(bool boss, double speed)
    {
        if (boss) _encounter.BossInfos.Add(Boss(new long[] { 10 }, new long[] { 10 }));
        var timeout = boss ? 3 : 0.5;
        var logs = new[]
        {
            Log(0, _7_0LogParsing.EnterCombatId),
            Log(0.1, _7_0LogParsing._damageEffectId, target: Npc(10)),
            Log(1, _7_0LogParsing.ExitCombatId),
            Log(1.1, _7_0LogParsing._damageEffectId, source: Npc(10)),
            Log(1.2, _7_0LogParsing._damageEffectId, target: Npc(10)),
            Log(1.2 + timeout - 0.01, 0),
            Log(1.2 + timeout + 0.01, 0),
            Log(10, _7_0LogParsing.EnterCombatId),
            Log(11, _7_0LogParsing.ExitCombatId),
            Log(11.6, 0)
        };
        var historical = new CombatDetector();
        var historicalStops = new List<int>();
        var replayStops = new List<int>();
        var index = 0;
        historical.AlertExitCombatTimedOut += (_, realtime) => { Assert.That(realtime, Is.False); historicalStops.Add(index); };
        _detector.AlertExitCombatTimedOut += (_, realtime) => { Assert.That(realtime, Is.True); replayStops.Add(index); };
        using var context = LogReplayContext.Begin(Start, speed);
        try
        {
            for (; index < logs.Length; index++)
                Assert.That(_detector.CheckForCombatState(logs[index], true),
                    Is.EqualTo(historical.CheckForCombatState(logs[index])), "Record " + index);
            Assert.That(replayStops, Is.EqualTo(new[] { 6, 9 }));
            Assert.That(replayStops, Is.EqualTo(historicalStops));
            var timer = (Timer)typeof(CombatDetector).GetField("_timeoutTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_detector)!;
            Assert.That(timer.Enabled, Is.False, "A replay must not race a real elapsed callback against its batch.");
        }
        finally { historical.Reset(); }
    }

    [Test]
    public void EmptyReplayFrameCanFinishAnIdleExitWithoutARealTimer()
    {
        using var context = LogReplayContext.Begin(Start.AddSeconds(10), 30);
        var stops = new List<bool>();
        _detector.AlertExitCombatTimedOut += (_, realtime) => stops.Add(realtime);
        _detector.CheckForCombatState(Log(0, _7_0LogParsing.EnterCombatId), true);
        _detector.CheckForCombatState(Log(1, _7_0LogParsing.ExitCombatId), true);
        typeof(CombatDetector).GetMethod("CheckReplayTimeout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_detector, new object[] { 0L });
        Assert.That(stops, Is.EqualTo(new[] { true }));
        Assert.That(_detector.InCombat, Is.False);
    }

    private CombatState Check(double seconds, ulong effect, Entity source = null, Entity target = null, ulong ability = 0) =>
        _detector.CheckForCombatState(Log(seconds, effect, source, target, ability));

    private ParsedLogEntry Log(double seconds, ulong effect, Entity source = null, Entity target = null, ulong ability = 0) => new()
        {
            TimeStamp = Start.AddSeconds(seconds),
            SourceInfo = new EntityInfo { Entity = source ?? _player },
            TargetInfo = new EntityInfo { Entity = target ?? _player },
            Effect = new Effect { EffectId = effect }, AbilityId = ability
        };

    private static Entity Npc(long id) => new() { Id = id, LogId = id, Name = "NPC " + id };
    private static BossInfo Boss(long[] targets, long[] required) => new()
    {
        EncounterName = "Boss", TargetIds = new List<long>(targets), TargetsRequiredForKill = new List<long>(required)
    };
}
