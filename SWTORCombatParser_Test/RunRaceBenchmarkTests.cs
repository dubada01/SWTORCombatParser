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
public class RunRaceBenchmarkTests
{
    private static readonly DateTime Entered = new(2026, 11, 7, 18, 0, 0);
    private LogState _previousState;
    private string _temporaryDirectory;
    private RunGhostStore _store;
    private RunRaceService _service;
    private readonly ConcurrentQueue<Request> _requests = new();

    private sealed record Request(RunCohort Cohort, string Boss, CancellationToken Cancellation,
        TaskCompletionSource<TimeTrialSummary> Completion);

    [SetUp]
    public void SetUp()
    {
        _requests.Clear();
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.CurrentState = new LogState();
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "orbs-run-benchmark-tests-" + Guid.NewGuid().ToString("N"));
        _store = new RunGhostStore(Path.Combine(_temporaryDirectory, "ghosts.json"));
        _service = new RunRaceService(_store, (cohort, boss, cancellation) =>
        {
            // Deliberately ignore cancellation: the service must fence late backend responses.
            var completion = new TaskCompletionSource<TimeTrialSummary>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Enqueue(new Request(cohort, boss, cancellation, completion));
            return completion.Task;
        }, loadRuns: (_, _, _, _) => Task.FromResult<RunReferences?>(null), isOffline: () => false);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
        foreach (var request in _requests) request.Completion.TrySetCanceled();
        CombatLogStateBuilder.CurrentState = _previousState;
        if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
    }

    [Test]
    public async Task LeaderboardCarriesIndependentStartEndAndSampleCountsIntoReference()
    {
        var area = Flashpoint();
        Import(area, Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        Assert.That(_service.Snapshot.ReferenceLoading, Is.True);

        foreach (var request in _requests)
            request.Completion.SetResult(request.Boss switch
            {
                "First" => Summary(8, 24, 123),
                "Optional" => Summary(60, 95, 27),
                _ => Summary(130, 190, 88)
            });
        await Until(() => !_service.Snapshot.ReferenceLoading);

        var snapshot = _service.Snapshot;
        var first = snapshot.Checkpoints.Single(checkpoint => checkpoint.Boss == "First");
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsLeaderboardReference, Is.True);
            Assert.That(snapshot.ReferenceLabel, Is.EqualTo("Leaderboard · fastest 10%"));
            Assert.That(snapshot.ReferenceDurationSeconds, Is.EqualTo(190));
            Assert.That(snapshot.Ghost, Is.Null, "Independent API checkpoints are not a fictional complete personal run.");
            Assert.That(first.StartSeconds, Is.EqualTo(10));
            Assert.That(first.Seconds, Is.EqualTo(30));
            Assert.That(first.GhostStartSeconds, Is.EqualTo(8));
            Assert.That(first.GhostSeconds, Is.EqualTo(24));
            Assert.That(first.ReferenceSamples, Is.EqualTo(123));
            Assert.That(first.DeltaSeconds, Is.EqualTo(6));
            Assert.That(snapshot.Checkpoints.Single(checkpoint => checkpoint.Boss == "Last").DeltaSeconds, Is.Null);
            Assert.That(_requests.All(request => request.Cohort == new RunCohort(area.Name, "Veteran", "4")), Is.True);
        });
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Task.Delay(60);
        Assert.That(_requests.Count, Is.EqualTo(3), "Selecting the active reference must reuse the current results.");
    }

    [Test]
    public async Task ReturningToPersonalBestCancelsAndSuppressesLateLeaderboardResponses()
    {
        Import(Flashpoint(), Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        var pending = _requests.ToArray();
        _service.SetGhostMode("Personal best");
        var personalSnapshot = _service.Snapshot;
        Assert.That(pending.All(request => request.Cancellation.IsCancellationRequested), Is.True);

        await CompleteLate(pending, 900);
        Assert.Multiple(() =>
        {
            Assert.That(_service.Snapshot.Cohort, Is.EqualTo(personalSnapshot.Cohort));
            Assert.That(_service.Snapshot.ReferenceDurationSeconds, Is.Null, "Cancelled results must not replace personal references; checkpoint autosaves may publish an update.");
            Assert.That(_service.Snapshot.IsLeaderboardReference, Is.False);
            Assert.That(_service.Snapshot.ReferenceLoading, Is.False);
            Assert.That(_service.Snapshot.Checkpoints.All(checkpoint => checkpoint.ReferenceSamples is null), Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ANewVisitOrCohortCannotBeOverwrittenByThePreviousRequest(bool differentCohort)
    {
        Import(Flashpoint(), Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        var oldRequests = _requests.ToArray();
        var nextVisit = Entered.AddMinutes(6);
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[Entered.AddMinutes(5)] =
            new EncounterInfo { Name = "Fleet", EncounterType = EncounterType.OpenWorld };
        var area = Flashpoint(differentCohort ? "Different flashpoint" : "Test flashpoint");
        Import(area, nextVisit);
        await Until(() => _requests.Count == 6);
        var currentRequests = _requests.ToArray().Skip(3).ToArray();
        foreach (var request in currentRequests) request.Completion.SetResult(Summary(10, 200, 5));
        await Until(() => !_service.Snapshot.ReferenceLoading);
        var currentSnapshot = _service.Snapshot;

        await CompleteLate(oldRequests, 900);
        Assert.Multiple(() =>
        {
            Assert.That(oldRequests.All(request => request.Cancellation.IsCancellationRequested), Is.True);
            // A checkpoint autosave can publish a fresh, equivalent snapshot.
            // Assert the selected visit's data stays intact, not object identity.
            Assert.That(_service.Snapshot.Checkpoints, Is.EqualTo(currentSnapshot.Checkpoints));
            Assert.That(_service.Snapshot.ElapsedSeconds, Is.EqualTo(currentSnapshot.ElapsedSeconds));
            Assert.That(_service.Snapshot.Wipes, Is.EqualTo(currentSnapshot.Wipes));
            Assert.That(_service.Snapshot.Entered, Is.EqualTo(nextVisit));
            Assert.That(_service.Snapshot.Cohort.Encounter, Is.EqualTo(area.Name));
            Assert.That(_service.Snapshot.ReferenceDurationSeconds, Is.EqualTo(200));
            Assert.That(_service.Snapshot.Checkpoints.All(checkpoint => checkpoint.ReferenceSamples == 5), Is.True);
        });
    }

    [Test]
    public async Task MissingInvalidAndFailedBossResponsesKeepValidCheckpoints()
    {
        var area = Flashpoint();
        area.BossInfos.Add(new BossInfo { EncounterName = "Negative" });
        area.BossInfos.Add(new BossInfo { EncounterName = "Reversed" });
        Import(area, Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 5);
        foreach (var request in _requests)
        {
            if (request.Boss == "Last") request.Completion.SetException(new IOException("Fixture backend unavailable"));
            else request.Completion.SetResult(request.Boss switch
            {
                "First" => Summary(8, 24, 42),
                "Optional" => Summary(40, 80, 0),
                "Negative" => Summary(-1, 90, 42),
                _ => Summary(100, 50, 42)
            });
        }
        await Until(() => !_service.Snapshot.ReferenceLoading);
        var snapshot = _service.Snapshot;
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Checkpoints.Single(checkpoint => checkpoint.Boss == "First").ReferenceSamples, Is.EqualTo(42));
            Assert.That(snapshot.Checkpoints.Where(checkpoint => checkpoint.Boss != "First").All(checkpoint => checkpoint.GhostSeconds is null), Is.True);
            Assert.That(snapshot.Checkpoints, Has.Count.EqualTo(5), "Missing reference data must not remove live boss checkpoints.");
            Assert.That(snapshot.ReferenceDurationSeconds, Is.EqualTo(24));
            Assert.That(snapshot.Status, Does.Contain("1/5 checkpoints"));
            Assert.That(snapshot.Kills, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnEmptyReferenceLeavesTheRunUsableAndReportsUnavailableTimings()
    {
        Import(Flashpoint(), Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        foreach (var request in _requests) request.Completion.SetResult(new TimeTrialSummary());
        await Until(() => !_service.Snapshot.ReferenceLoading);
        Assert.Multiple(() =>
        {
            Assert.That(_service.Snapshot.ReferenceDurationSeconds, Is.Null);
            Assert.That(_service.Snapshot.Status, Does.Contain("unavailable"));
            Assert.That(_service.Snapshot.Kills, Is.EqualTo(1));
            Assert.That(_service.Snapshot.Checkpoints, Has.Count.EqualTo(3));
            Assert.That(_service.Snapshot.Checkpoints.All(checkpoint => checkpoint.GhostSeconds is null), Is.True);
        });
    }

    [Test]
    public async Task IndependentBackendCancellationPreservesOtherCheckpointsAndFinishesLoading()
    {
        Import(Flashpoint(), Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        foreach (var request in _requests)
        {
            if (request.Boss == "Optional")
                request.Completion.SetException(new OperationCanceledException("The backend's own timeout elapsed."));
            else request.Completion.SetResult(Summary(8, request.Boss == "First" ? 24 : 190, 42));
        }
        await Until(() => !_service.Snapshot.ReferenceLoading);
        Assert.Multiple(() =>
        {
            Assert.That(_requests.All(request => !request.Cancellation.IsCancellationRequested), Is.True);
            Assert.That(_service.Snapshot.Checkpoints.Single(checkpoint => checkpoint.Boss == "Optional").GhostSeconds, Is.Null);
            Assert.That(_service.Snapshot.Checkpoints.Count(checkpoint => checkpoint.ReferenceSamples == 42), Is.EqualTo(2));
            Assert.That(_service.Snapshot.ReferenceDurationSeconds, Is.EqualTo(190));
            Assert.That(_service.Snapshot.Status, Does.Contain("2/3 checkpoints"));
            Assert.That(_service.Snapshot.Kills, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task StoppingLiveMonitoringClearsTheActiveBossAndResumingClearsPausedState()
    {
        // Seed a live visit without launching the parser, reading settings, or opening an app.
        var area = Flashpoint();
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[Entered] = area;
        var tracker = new RunRaceTracker();
        tracker.Begin(new RunCohort(area.Name, area.Difficutly, area.NumberOfPlayer), area.BossInfos.Select(boss => boss.EncounterName), Entered);
        tracker.Advance(Entered.AddSeconds(20));
        void Seed(string field, object value) => typeof(RunRaceService)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_service, value);
        Seed("_tracker", tracker);
        Seed("_anchorLogTime", Entered.AddSeconds(20));
        Seed("_anchorClock", Stopwatch.GetTimestamp());
        var monitor = (CombatMonitorViewModel)RuntimeHelpers.GetUninitializedObject(typeof(CombatMonitorViewModel));
        var previousLive = CombatMonitorViewModel.IsLiveParseActive();
        var previousCombat = CombatIdentifier.CurrentCombat;
        try
        {
            monitor.LiveParseActive = true;
            _service.Start();
            await Until(() => _service.Snapshot.Cohort != null);
            var combat = new Combat
            {
                ParentEncounter = area, EncounterBossDifficultyParts = ("First", area.Difficutly, area.NumberOfPlayer),
                StartTime = Entered.AddSeconds(10), EndTime = Entered.AddSeconds(20)
            };
            CombatSelectionMonitor.InProgressCombatSeleted(combat);
            Assert.That(_service.Snapshot.ActiveBossName, Is.EqualTo("First"));
            Assert.That(_service.Snapshot.ActiveBossStartSeconds, Is.EqualTo(10));

            // Match the application's ordering: flag becomes false before stop notification.
            monitor.LiveParseActive = false;
            _service.SetMonitoringState(false);
            Assert.Multiple(() =>
            {
                Assert.That(_service.Snapshot.IsPaused, Is.True);
                Assert.That(_service.Snapshot.ActiveBossName, Is.Null);
                Assert.That(_service.Snapshot.ActiveBossStartSeconds, Is.Null);
                Assert.That(_service.Snapshot.Status, Does.Contain("stopped").IgnoreCase);
                Assert.That(_service.Snapshot.Cohort.Encounter, Is.EqualTo(area.Name));
            });
            monitor.LiveParseActive = true;
            _service.SetMonitoringState(true);
            Assert.That(_service.Snapshot.IsPaused, Is.False);
            Assert.That(_service.Snapshot.ActiveBossName, Is.Null, "Resuming must await a fresh active-combat observation.");
        }
        finally
        {
            _service.Dispose();
            monitor.LiveParseActive = previousLive;
            CombatIdentifier.CurrentCombat = previousCombat;
        }
    }

    [Test]
    public async Task SavedPersonalRoutesRemainAvailableWhenOnlineReferencesAreUnavailable()
    {
        var area = Flashpoint();
        var cohort = new RunCohort(area.Name, area.Difficutly, area.NumberOfPlayer);
        var ghostStart = Entered.AddDays(-1);
        var catalog = new RunGhost(cohort, ghostStart, ghostStart.AddSeconds(210), "",
            new[] { new RunSplit("First", 10, 40), new RunSplit("Optional", 60, 90), new RunSplit("Last", 150, 210) });
        var custom = new RunGhost(cohort, ghostStart.AddHours(1), ghostStart.AddHours(1).AddSeconds(150), "",
            new[] { new RunSplit("First", 10, 35), new RunSplit("Last", 100, 150) }, true);
        _store.Save(catalog);
        _store.Save(custom);
        _service.Start();
        Import(area, Entered);
        await Until(() => _service.Snapshot.Ghost != null);
        Assert.That(_service.Snapshot.Routes.Select(r => r.Key), Does.Contain(catalog.RouteKey));

        _service.SetRoute(custom.RouteKey);
        Assert.Multiple(() =>
        {
            Assert.That(_service.Snapshot.Ghost.RouteKey, Is.EqualTo(custom.RouteKey));
            Assert.That(_service.Snapshot.Checkpoints.Select(checkpoint => checkpoint.Boss), Is.EqualTo(new[] { "First", "Last" }));
            Assert.That(_service.Snapshot.ReferenceDurationSeconds, Is.EqualTo(150));
            Assert.That(_service.Snapshot.IsLeaderboardReference, Is.False);
            Assert.That(_requests, Is.Empty);
        });
        _service.SetGhostMode("Previous run");
        await Task.Delay(60);
        Assert.That(_requests, Is.Empty);

        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        _service.SetRoute(custom.RouteKey);
        Assert.That(_service.Snapshot.RouteKey, Is.EqualTo(custom.RouteKey), "The detected route also filters the leaderboard's visible bosses.");
    }

    [Test]
    public async Task DisposingWhileLoadingSuppressesCompletionAndCancelsTheRequest()
    {
        Import(Flashpoint(), Entered);
        _service.SetGhostMode(RunRaceService.LeaderboardMode);
        await Until(() => _requests.Count == 3);
        var pending = _requests.ToArray();
        var before = _service.Snapshot;
        _service.Dispose();
        await CompleteLate(pending, 900);
        Assert.That(pending.All(request => request.Cancellation.IsCancellationRequested), Is.True);
        Assert.That(_service.Snapshot, Is.SameAs(before));
    }

    private static TimeTrialSummary Summary(int start, int end, int samples) =>
        new() { StartSeconds = start, EndSeconds = end, SampleCount = samples };

    private static async Task CompleteLate(IEnumerable<Request> pending, int end)
    {
        foreach (var request in pending) request.Completion.SetResult(Summary(5, end, 999));
        // Allow the already-started asynchronous backend continuations to attempt publication.
        await Task.Delay(100);
    }

    private static async Task Until(Func<bool> predicate)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(5)) Assert.Fail("Timed out waiting for the run reference state.");
            await Task.Delay(10);
        }
    }

    private void Import(EncounterInfo area, DateTime entered)
    {
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[entered] = area;
        _service.ImportHistoricalCombats(new[]
        {
            new Combat
            {
                ParentEncounter = area, EncounterBossDifficultyParts = ("First", area.Difficutly, area.NumberOfPlayer),
                StartTime = entered.AddSeconds(10), EndTime = entered.AddSeconds(30), BossKillOverride = true
            }
        });
    }

    private static EncounterInfo Flashpoint(string name = "Test flashpoint") => new()
    {
        Name = name, EncounterType = EncounterType.Flashpoint, Difficutly = "Veteran", NumberOfPlayer = "4",
        BossInfos = new List<BossInfo>
        {
            new() { EncounterName = "First" }, new() { EncounterName = "Optional" }, new() { EncounterName = "Last" }
        }
    };
}
