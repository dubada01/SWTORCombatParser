using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.RunRace;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class RunReferenceTests
{
    private static readonly DateTime Entered = new(2026, 11, 7, 12, 0, 0);
    private static readonly RunCohort Cohort = new("Fixture FP", "Veteran", "4");
    private LogState _previous;
    private string _directory;
    private RunGhostStore _store;
    private RunRaceService _service;
    private EncounterInfo _area;

    [SetUp]
    public void SetUp()
    {
        _previous = CombatLogStateBuilder.CurrentState;
        var state = new LogState();
        typeof(LogState).GetProperty(nameof(LogState.LocalPlayer))!.SetValue(state, new Entity { Name = "Local", IsLocalPlayer = true, IsCharacter = true });
        CombatLogStateBuilder.CurrentState = state;
        _directory = Path.Combine(Path.GetTempPath(), "OrbsRunReferences_" + Guid.NewGuid().ToString("N"));
        _store = new RunGhostStore(Path.Combine(_directory, "ghosts.json"));
        _area = new EncounterInfo { Name = Cohort.Encounter, Difficutly = Cohort.Difficulty, NumberOfPlayer = "4",
            EncounterType = EncounterType.Flashpoint, BossInfos = new List<BossInfo> {
                new() { EncounterName = "First" }, new() { EncounterName = "Optional" }, new() { EncounterName = "Last" } } };
    }

    [TearDown]
    public void TearDown()
    {
        _service?.Dispose();
        CombatLogStateBuilder.CurrentState = _previous;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static RunGhost Ghost(DateTime entered, int seconds, string player = "Local") => new(Cohort, entered,
        entered.AddSeconds(seconds), player, new[] { new RunSplit("First", 5, 30), new RunSplit("Last", seconds - 20, seconds) });
    private static RunReferences References(RunGhost pb, RunGhost latest) => new(new[] {
        new RunReferenceRoute(pb.RouteKey, pb.Splits.Select(s => s.Boss).ToArray(), 20, pb, latest) });
    private void Import(DateTime entered, bool complete = false)
    {
        CombatLogStateBuilder.CurrentState.EncounterEnteredInfo[entered] = _area;
        var combats = new List<Combat> { new() { ParentEncounter = _area, StartTime = entered.AddSeconds(10),
            EndTime = entered.AddSeconds(30), EncounterBossDifficultyParts = ("First", "Veteran", "4"), BossKillOverride = true } };
        if (complete) combats.Add(new Combat { ParentEncounter = _area, StartTime = entered.AddSeconds(90), EndTime = entered.AddSeconds(120),
            EncounterBossDifficultyParts = ("Last", "Veteran", "4"), BossKillOverride = true });
        _service.ImportHistoricalCombats(combats);
    }
    private static async Task Until(Func<bool> check)
    {
        var timer = Stopwatch.StartNew();
        while (!check()) { if (timer.Elapsed.TotalSeconds > 5) Assert.Fail("Run reference state timed out."); await Task.Delay(10); }
    }

    [Test]
    public async Task OfflineKillsPersistProgressThenCompletionAndNeverFetchReferences()
    {
        var requests = 0;
        _service = new RunRaceService(_store, (_, _, _) => { requests++; return Task.FromResult(new TimeTrialSummary()); },
            (_, _, _, _) => { requests++; return Task.FromResult<RunReferences?>(null); }, () => true);
        Import(Entered);
        await Until(() => _store.Read().Count == 1);
        Assert.That(_store.Read().Single().IsComplete, Is.False);
        Assert.That(_service.Snapshot.Ghost, Is.Null);
        Import(Entered, true);
        _service.Dispose();
        var saved = new RunGhostStore(Path.Combine(_directory, "ghosts.json")).Read();
        Assert.That(saved, Has.Count.EqualTo(1));
        Assert.That(saved.Single().IsComplete, Is.True);
        Assert.That(saved.Single().DurationSeconds, Is.EqualTo(120));
        // A delayed earlier checkpoint cannot replace the completed run.
        _store.Save(saved.Single() with { IsComplete = false, Splits = saved.Single().Splits.Take(1).ToArray() });
        Assert.That(_store.Read().Single().IsComplete, Is.True);
        Assert.That(requests, Is.Zero);
    }

    [Test]
    public async Task OnlinePersonalBestAndLatestAreCachedAndRemainUsableOffline()
    {
        var pb = Ghost(Entered.AddDays(-2), 100);
        var latest = Ghost(Entered.AddDays(-1), 150);
        var offline = false;
        var requests = 0;
        _service = new RunRaceService(_store, loadRuns: (cohort, player, entered, _) => {
            Assert.That(cohort, Is.EqualTo(Cohort)); Assert.That(player, Is.EqualTo("Local")); Assert.That(entered, Is.EqualTo(Entered));
            Interlocked.Increment(ref requests); return Task.FromResult<RunReferences?>(References(pb, latest));
        }, isOffline: () => offline);
        Import(Entered);
        await Until(() => _service.Snapshot.Ghost?.DurationSeconds == 100 && _store.Read().Count(r => r.IsComplete) == 2);
        _service.SetGhostMode("Latest run");
        await Until(() => !_service.Snapshot.ReferenceLoading && _service.Snapshot.Ghost?.DurationSeconds == 150);
        offline = true;
        var previousRequests = requests;
        _service.RefreshReference();
        Assert.That(_service.Snapshot.Ghost!.DurationSeconds, Is.EqualTo(150));
        Assert.That(_service.Snapshot.Status, Does.Contain("Offline"));
        Assert.That(requests, Is.EqualTo(previousRequests));
        _service.Dispose();
        _service = new RunRaceService(new RunGhostStore(Path.Combine(_directory, "ghosts.json")), isOffline: () => true);
        _service.Start();
        Import(Entered);
        await Until(() => _service.Snapshot.Ghost?.DurationSeconds == 100);
    }

    [Test]
    public async Task OfflineSwitchFencesLateOnlineResponses()
    {
        var reply = new TaskCompletionSource<RunReferences?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requested = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var offline = false;
        _service = new RunRaceService(_store, loadRuns: (_, _, _, token) => { requested.TrySetResult(token); return reply.Task; }, isOffline: () => offline);
        Import(Entered);
        var cancellation = await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        offline = true;
        // Settings changes can cancel an active request; Refresh deliberately waits for one to finish.
        typeof(RunRaceService).GetMethod("OnSettingsChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(_service, new object[] { "offline_mode" });
        Assert.That(cancellation.IsCancellationRequested, Is.True);
        reply.SetResult(References(Ghost(Entered.AddDays(-1), 100), Ghost(Entered.AddDays(-1), 100)));
        await Task.Delay(60);
        Assert.That(_service.Snapshot.Ghost, Is.Null);
        Assert.That(_service.Snapshot.ReferenceLoading, Is.False);
        Assert.That(_store.Read().Any(r => r.IsComplete), Is.False);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(respond(request));
    }

    [Test]
    public async Task ClientUsesExactContextAndRejectsOtherCharactersAndCurrentVisit()
    {
        var pb = Ghost(Entered.AddDays(-1), 100, "OtherPlayer");
        using var http = new HttpClient(new Handler(request => {
            var uri = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);
            Assert.That(uri, Does.Contain("/trial_leaderboard/runs?encounter=Fixture FP&difficulty=Veteran&playerCount=4&playerName=Local&excludeEntered="));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(References(pb, Ghost(Entered, 150))) };
        }));
        var response = await new RunReferenceClient(http).GetAsync("https://fixture.invalid/api", Cohort, "Local", Entered, CancellationToken.None);
        Assert.That(response!.Routes, Has.Count.EqualTo(1));
        Assert.That(response.Routes.Single().PersonalBest, Is.Null);
        Assert.That(response.Routes.Single().Latest, Is.Null);
    }

    [Test]
    public async Task ClientDeserializesCompletedPersonalRunsFromTheApiContract()
    {
        var ghost = Ghost(Entered.AddDays(-1), 120);
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = JsonContent.Create(new { routes = new[] { new { key = ghost.RouteKey, bosses = ghost.Splits.Select(s => s.Boss),
                sampleCount = 10, personalBest = new { cohort = ghost.Cohort, entered = ghost.Entered, completed = ghost.Completed,
                    player = ghost.Player, splits = ghost.Splits, isComplete = true, isCustomRoute = true }, latest = (object)null } }, ready = true }) }));
        var response = await new RunReferenceClient(http).GetAsync("https://fixture.invalid/api", Cohort, "Local", Entered, CancellationToken.None);
        Assert.That(response!.Routes.Single().PersonalBest!.DurationSeconds, Is.EqualTo(120));
        Assert.That(response.Routes.Single().PersonalBest!.RouteKey, Is.EqualTo(ghost.RouteKey));
    }

    [Test]
    public async Task MissingApiEndpointFallsBackCleanly()
    {
        using var http = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        Assert.That(await new RunReferenceClient(http).GetAsync("https://fixture.invalid/api", Cohort, "Local", Entered, CancellationToken.None), Is.Null);
    }
}
