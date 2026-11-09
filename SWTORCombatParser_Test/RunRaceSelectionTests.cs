using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class RunRaceSelectionTests
{
    private static readonly DateTime Entered = new(2026, 11, 7, 18, 0, 0);
    private static readonly DateTime SecondVisit = Entered.AddMinutes(10);
    private LogState _previousState;
    private Combat _previousCombat;
    private bool _previousLive;
    private string _directory;
    private RunRaceService _service;
    private CombatMonitorViewModel _monitor;
    private EncounterInfo _area;

    [SetUp]
    public void Setup()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        _previousCombat = CombatIdentifier.CurrentCombat;
        _previousLive = CombatMonitorViewModel.IsLiveParseActive();
        _monitor = (CombatMonitorViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CombatMonitorViewModel));
        _monitor.LiveParseActive = false;
        CombatLogStateBuilder.CurrentState = new LogState();
        _directory = Path.Combine(Path.GetTempPath(), "orbs-race-selection-" + Guid.NewGuid().ToString("N"));
        _service = new RunRaceService(new RunGhostStore(Path.Combine(_directory, "ghosts.json")), isOffline: () => true);
        _service.Start();
        _area = new EncounterInfo
        {
            Name = "Selection flashpoint", EncounterType = EncounterType.Flashpoint,
            Difficutly = "Veteran", NumberOfPlayer = "4",
            BossInfos = new List<BossInfo> { new() { EncounterName = "First" }, new() { EncounterName = "Last" } }
        };
        var entries = CombatLogStateBuilder.CurrentState.EncounterEnteredInfo;
        entries[Entered] = _area;
        entries[Entered.AddMinutes(5)] = new EncounterInfo { Name = "Fleet", EncounterType = EncounterType.OpenWorld };
        entries[SecondVisit] = _area;
    }

    [TearDown]
    public void Cleanup()
    {
        _service.Dispose();
        _monitor.LiveParseActive = _previousLive;
        CombatIdentifier.CurrentCombat = _previousCombat;
        CombatLogStateBuilder.CurrentState = _previousState;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Test]
    public void SelectingEarlierCombatShowsItsWholeEncounterVisitAndCanSwitchBack()
    {
        var first = Attempt(Entered, "First", 10, 30);
        var last = Attempt(Entered, "Last", 90, 120);
        var newer = Attempt(SecondVisit, "First", 20, 45);
        _service.ImportHistoricalCombats(new[] { first, last, newer, Attempt(SecondVisit, "Last", 130, 180) });
        Assert.That(_service.Snapshot.Entered, Is.EqualTo(SecondVisit));

        // A cached/reloaded combat has a new object identity but belongs to the same visit.
        CombatSelectionMonitor.SelectCombat(Attempt(Entered, "First", 10, 30), false);
        Assert.Multiple(() =>
        {
            Assert.That(_service.Snapshot.Entered, Is.EqualTo(Entered));
            Assert.That(_service.Snapshot.ElapsedSeconds, Is.EqualTo(120));
            Assert.That(_service.Snapshot.Kills, Is.EqualTo(2));
            Assert.That(_service.Snapshot.IsComplete, Is.True);
            Assert.That(_service.Snapshot.IsHistorical, Is.True);
            Assert.That(_service.Snapshot.Checkpoints.Single(c => c.Boss == "Last").Seconds, Is.EqualTo(120));
        });

        CombatSelectionMonitor.SelectCombat(newer, false);
        Assert.That(_service.Snapshot.Entered, Is.EqualTo(SecondVisit));
        Assert.That(_service.Snapshot.ElapsedSeconds, Is.EqualTo(180));
    }

    [Test]
    public void SelectingAnIncompleteVisitRetainsItsWipesAndDoesNotBorrowLaterKills()
    {
        var wipe = Attempt(Entered, "First", 10, 30, false);
        _service.ImportHistoricalCombats(new[] { wipe, Attempt(Entered, "First", 40, 70), Attempt(SecondVisit, "Last", 90, 120) });
        CombatSelectionMonitor.SelectCombat(wipe, false);
        Assert.Multiple(() =>
        {
            Assert.That(_service.Snapshot.Wipes, Is.EqualTo(1));
            Assert.That(_service.Snapshot.Kills, Is.EqualTo(1));
            Assert.That(_service.Snapshot.Checkpoints.Single(c => c.Boss == "First").Attempts, Is.EqualTo(2));
            Assert.That(_service.Snapshot.Checkpoints.Single(c => c.Boss == "Last").Seconds, Is.Null);
            Assert.That(_service.Snapshot.IsComplete, Is.False);
            Assert.That(_service.Snapshot.ActiveBossName, Is.Null);
        });
    }

    [Test]
    public void UnsupportedSelectionAndLoadingAnotherLogClearThePreviousResults()
    {
        var first = Attempt(Entered, "First", 10, 30);
        _service.ImportHistoricalCombats(new[] { first });
        CombatSelectionMonitor.SelectCombat(first, false);
        CombatSelectionMonitor.SelectCombat(new Combat
        {
            ParentEncounter = new EncounterInfo { Name = "Fleet", EncounterType = EncounterType.OpenWorld },
            StartTime = Entered.AddMinutes(6), EndTime = Entered.AddMinutes(7)
        }, false);
        Assert.That(_service.Snapshot.Cohort, Is.Null);
        Assert.That(_service.Snapshot.Status, Does.Contain("selected encounter"));
        CombatSelectionMonitor.SelectCombat(first, false);
        _service.ImportHistoricalCombats(Array.Empty<Combat>());
        CombatSelectionMonitor.SelectCombat(first, false);
        Assert.That(_service.Snapshot.Cohort, Is.Null, "A previous log's cached tracker must not survive a new import.");
    }

    [Test]
    public void HistoricalReviewSurvivesLiveUpdatesAndCompletionWithoutLosingLiveProgress()
    {
        var first = Attempt(Entered, "First", 10, 30);
        var ongoing = Attempt(SecondVisit, "First", 20, 45);
        _service.ImportHistoricalCombats(new[] { first, Attempt(Entered, "Last", 90, 120), ongoing });
        _monitor.LiveParseActive = true;
        Invoke("OnLog", new ParsedLogEntry { TimeStamp = SecondVisit.AddSeconds(50), LogName = "test.txt" });
        CombatSelectionMonitor.SelectCombat(first, false);
        CombatSelectionMonitor.InProgressCombatSeleted(Attempt(SecondVisit, "Last", 60, 70));
        Invoke("OnLog", new ParsedLogEntry { TimeStamp = SecondVisit.AddSeconds(80), LogName = "test.txt" });
        var finished = Attempt(SecondVisit, "Last", 60, 150);
        Invoke("OnCombatFinished", finished);
        CombatSelectionMonitor.SelectCompleteCombat(finished); // Automatic completion, not a row click.
        Assert.That(_service.Snapshot.Entered, Is.EqualTo(Entered));
        Assert.That(_service.Snapshot.ElapsedSeconds, Is.EqualTo(120));

        CombatSelectionMonitor.SelectCombat(ongoing, true);
        Assert.Multiple(() =>
        {
            Assert.That(_service.Snapshot.Entered, Is.EqualTo(SecondVisit));
            Assert.That(_service.Snapshot.ElapsedSeconds, Is.EqualTo(150));
            Assert.That(_service.Snapshot.Kills, Is.EqualTo(2));
            Assert.That(_service.Snapshot.IsComplete, Is.True);
            Assert.That(_service.Snapshot.IsHistorical, Is.False);
        });
    }

    [Test]
    public void ReviewingHistoricalCombatKeepsIncrementalLiveUpdatesInTheLiveShell()
    {
        var source = new Entity { Name = "Player", Id = 11, LogId = 11, IsCharacter = true };
        var target = new Entity { Name = "Enemy", Id = 12, LogId = 12 };
        ParsedLogEntry Hit(int line, DateTime time) => new()
        {
            LogLineNumber = line, TimeStamp = time,
            SourceInfo = new EntityInfo { Entity = source, MaxHP = 10000 },
            TargetInfo = new EntityInfo { Entity = target, MaxHP = 10000 },
            Ability = "Attack", AbilityId = 100,
            Effect = new Effect { EffectType = EffectType.Apply, EffectId = _7_0LogParsing._damageEffectId },
            Value = new Value { DblValue = 100, EffectiveDblValue = 100, MitigatedDblValue = 100 }
        };
        // Isolate combat metadata from fixture bosses; this test concerns selection ownership.
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo.Clear();
        var historical = CombatIdentifier.GenerateCombatSnapshotFromLogs(new[] { Hit(1, Entered) });
        var live = CombatIdentifier.GenerateCombatSnapshotFromLogs(new[] { Hit(2, SecondVisit) });
        CombatIdentifier.CurrentCombat = live;
        _monitor.LiveParseActive = true;
        CombatSelectionMonitor.SelectCombat(historical, false);
        var updated = CombatIdentifier.GenerateCombatFromLogs(new[] { Hit(3, SecondVisit.AddSeconds(1)) });
        Assert.Multiple(() =>
        {
            Assert.That(updated, Is.SameAs(live));
            Assert.That(CombatIdentifier.CurrentCombat, Is.SameAs(live));
            Assert.That(live.AllLogs.Keys, Is.EquivalentTo(new long[] { 2, 3 }));
            Assert.That(historical.AllLogs.Keys, Is.EquivalentTo(new long[] { 1 }));
            Assert.That(historical.EndTime, Is.EqualTo(Entered));
        });
    }

    [Test]
    public async Task SelectingAnotherVisitCancelsAndFencesItsPreviousReferenceRequest()
    {
        _service.Dispose();
        var pending = new ConcurrentQueue<(DateTime Entered, CancellationToken Token, TaskCompletionSource<RunReferences> Reply)>();
        _service = new RunRaceService(new RunGhostStore(Path.Combine(_directory, "ghosts.json")), loadRuns: (_, _, entered, token) =>
        {
            var reply = new TaskCompletionSource<RunReferences>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Enqueue((entered, token, reply));
            return reply.Task;
        }, isOffline: () => false);
        typeof(LogState).GetProperty(nameof(LogState.LocalPlayer)).SetValue(CombatLogStateBuilder.CurrentState,
            new Entity { Name = "Local", IsCharacter = true, IsLocalPlayer = true });
        _service.Start();
        var first = Attempt(Entered, "First", 10, 30);
        var second = Attempt(SecondVisit, "First", 10, 45);
        _service.ImportHistoricalCombats(new[] { first, second });
        await Until(() => pending.Count == 1);
        CombatSelectionMonitor.SelectCombat(first, false);
        await Until(() => pending.Count == 2);
        CombatSelectionMonitor.SelectCombat(second, false);
        await Until(() => pending.Count == 3);
        var requests = pending.ToArray();
        requests[2].Reply.SetResult(new RunReferences(Array.Empty<RunReferenceRoute>()));
        await Until(() => !_service.Snapshot.ReferenceLoading);
        requests[0].Reply.SetResult(new RunReferences(Array.Empty<RunReferenceRoute>()));
        requests[1].Reply.SetResult(new RunReferences(Array.Empty<RunReferenceRoute>()));
        await Task.Delay(60);
        Assert.Multiple(() =>
        {
            Assert.That(requests[0].Token.IsCancellationRequested, Is.True);
            Assert.That(requests[1].Token.IsCancellationRequested, Is.True);
            Assert.That(requests[1].Entered, Is.EqualTo(Entered));
            Assert.That(requests[2].Entered, Is.EqualTo(SecondVisit));
            Assert.That(_service.Snapshot.Entered, Is.EqualTo(SecondVisit));
            Assert.That(_service.Snapshot.Checkpoints.Single(c => c.Boss == "First").Seconds, Is.EqualTo(45));
        });
    }

    private Combat Attempt(DateTime visit, string boss, int start, int end, bool killed = true) => new()
    {
        ParentEncounter = _area, StartTime = visit.AddSeconds(start), EndTime = visit.AddSeconds(end),
        EncounterBossDifficultyParts = (boss, _area.Difficutly, _area.NumberOfPlayer), BossKillOverride = killed,
        BossInfo = new BossInfo { TargetsRequiredForKill = new List<long>() }
    };

    private void Invoke(string method, params object[] arguments) => typeof(RunRaceService)
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_service, arguments);

    private static async Task Until(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed.TotalSeconds > 5) Assert.Fail("Race selection did not reach the expected state.");
            await Task.Delay(10);
        }
    }
}
