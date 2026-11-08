using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using Timer = System.Threading.Timer;
using Orbs.RunRace;

namespace SWTORCombatParser.Model.RunRace;

/// <summary>Owns race tracking independently of windows and timer configuration, with optional leaderboard references.</summary>
public sealed class RunRaceService : IDisposable
{
    public const string LeaderboardMode = "Leaderboard (fastest 10%)";
    private readonly object _gate = new();
    private readonly RunGhostStore _store;
    private readonly Func<RunCohort, string, CancellationToken, Task<TimeTrialSummary>> _loadBenchmark;
    private readonly Func<RunCohort, string, DateTime, CancellationToken, Task<RunReferences?>> _loadRuns;
    private readonly Func<bool> _isOffline;
    private readonly SemaphoreSlim _saveWorker = new(1, 1);
    private RunReferences? _online;
    private CancellationTokenSource? _runRequest;
    private int _runGeneration;
    private bool _runsLoading;
    private string _player = "";
    private string _onlineStatus = "";
    private IReadOnlyDictionary<string, TimeTrialSummary> _benchmarks = new Dictionary<string, TimeTrialSummary>();
    private CancellationTokenSource? _benchmarkRequest;
    private int _benchmarkGeneration;
    private bool _benchmarkLoading;
    private string? _activeBoss;
    private double? _activeBossStart;
    private RunRaceTracker? _tracker;
    private IReadOnlyList<RunGhost> _ghosts = Array.Empty<RunGhost>();
    private RunGhost? _ghost;
    private Timer? _timer;
    private bool _started, _disposed;
    private DateTime _anchorLogTime;
    private long _anchorClock;
    private string _ghostMode = "Personal best";
    private string _routeKey = "auto";
    private string? _automaticRoute;
    private bool _historicalPreview;
    private bool _monitoringStopped;
    private bool _isReplay;
    private bool _needsLiveReconcile = true;
    private RunRaceSnapshot _snapshot = RunRaceSnapshot.Empty;
    public event Action<RunRaceSnapshot>? Updated;
    public RunRaceSnapshot Snapshot { get { lock (_gate) return _snapshot; } }
    public string GhostMode { get { lock (_gate) return _ghostMode; } }

    public RunRaceService(RunGhostStore? store = null,
        Func<RunCohort, string, CancellationToken, Task<TimeTrialSummary>>? loadBenchmark = null,
        Func<RunCohort, string, DateTime, CancellationToken, Task<RunReferences?>>? loadRuns = null,
        Func<bool>? isOffline = null)
    {
        _store = store ?? new RunGhostStore();
        _loadBenchmark = loadBenchmark ?? ((cohort, boss, cancellation) =>
            API_Connection.GetTimeTrialSummary(boss, cohort.Encounter, cohort.Difficulty,
                cohort.GroupSize == "4" ? "4" : cohort.GroupSize + " Player", cancellation));
        _loadRuns = loadRuns ?? API_Connection.GetRunReferences;
        _isOffline = isOffline ?? (() => Settings.ReadSettingOfType<bool>("offline_mode"));
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started || _disposed) return;
            _started = true;
            CombatLogStateBuilder.AreaEntered += OnAreaEntered;
            CombatIdentifier.CombatFinished += OnCombatFinished;
            CombatSelectionMonitor.OnInProgressCombatSelected += OnLiveCombat;
            CombatLogStreamer.NewLineStreamed += OnLog;
            CombatLogStreamer.HistoricalLogsStarted += OnHistoryStarted;
            Settings.SettingsUpdated += OnSettingsChanged;
            _timer = new Timer(Tick, null, 1000, 1000);
        }
        _ = Task.Run(() =>
        {
            var ghosts = _store.Read();
            lock (_gate) { if (_disposed) return; _ghosts = ghosts; SelectGhost(); PublishLocked(); }
        });
    }

    public void SetGhostMode(string mode)
    {
        lock (_gate)
        {
            if (mode == "Previous run") mode = "Latest run";
            if (_disposed || mode is not ("Personal best" or "Latest run" or LeaderboardMode) || mode == _ghostMode) return;
            _ghostMode = mode;
            SelectGhost();
            ReloadReferencesLocked();
            PublishLocked();
        }
    }

    public void SetRoute(string key)
    {
        lock (_gate)
        {
            if (_disposed || !Routes().Any(route => route.Key == key)) return;
            _routeKey = key;
            SelectGhost();
            PublishLocked();
        }
    }

    public void RefreshReference()
    {
        lock (_gate)
        {
            if (_disposed || _benchmarkLoading || _runsLoading) return;
            ReloadReferencesLocked();
            PublishLocked();
        }
    }

    public void SetMonitoringState(bool monitoring)
    {
        lock (_gate)
        {
            if (_disposed) return;
            _monitoringStopped = !monitoring;
            _activeBoss = null;
            _activeBossStart = null;
            if (monitoring) _needsLiveReconcile = true;
            PublishLocked();
        }
    }

    private void OnSettingsChanged(string setting)
    {
        if (setting != "offline_mode") return;
        lock (_gate)
        {
            if (_disposed) return;
            ReloadReferencesLocked();
            PublishLocked();
        }
    }

    private static bool IsEligible(EncounterInfo encounter) => encounter != null && !encounter.IsOpenWorld &&
        encounter.BossInfos.Count > 0 && (encounter.EncounterType == EncounterType.Flashpoint ||
        encounter.EncounterType is EncounterType.Operation or EncounterType.Lair && encounter.Difficutly == "Story");

    private static RunCohort Cohort(EncounterInfo info) => new(info.Name, info.Difficutly, RunRouteIdentity.GroupSize(info.NumberOfPlayer));

    private static void Begin(RunRaceTracker tracker, EncounterInfo encounter, DateTime entered)
    {
        var definition = RunRouteCatalog.Find(encounter.Name);
        tracker.Begin(Cohort(encounter), encounter.BossInfos.Select(b => b.EncounterName), entered,
            definition?.RequireFirstBoss ?? encounter.EncounterType != EncounterType.Flashpoint, definition?.CompletionBoss);
    }

    private void OnAreaEntered(EncounterInfo encounter)
    {
        if (!CombatMonitorViewModel.IsLiveParseActive()) return;
        var entered = CombatLogStateBuilder.CurrentState.EncounterEnteredInfo
            .Where(pair => ReferenceEquals(pair.Value, encounter)).Select(pair => pair.Key).DefaultIfEmpty().Max();
        entered = EncounterSessionGrouping.VisitStart(CombatLogStateBuilder.CurrentState.EncounterEnteredInfo, entered);
        lock (_gate)
        {
            if (_disposed) return;
            if (_tracker != null && _tracker.Entered == entered && _tracker.Cohort == Cohort(encounter)) return;
            _needsLiveReconcile = false;
            _tracker = null;
            _ghost = null;
            _historicalPreview = false;
            _isReplay = LogReplayContext.IsActive;
            _activeBoss = null;
            _activeBossStart = null;
            _routeKey = "auto";
            _automaticRoute = null;
            _player = CombatLogStateBuilder.CurrentState.LocalPlayer?.Name ?? "";
            if (IsEligible(encounter) && entered != default)
            {
                _tracker = new RunRaceTracker();
                Begin(_tracker, encounter, entered);
                Anchor(entered);
                SelectGhost();
            }
            ReloadReferencesLocked();
            PublishLocked();
        }
    }

    private void OnLog(ParsedLogEntry log)
    {
        if (!CombatMonitorViewModel.IsLiveParseActive()) return;
        lock (_gate)
        {
            if (_disposed) return;
            var player = CombatLogStateBuilder.CurrentState.LocalPlayer?.Name ?? "";
            if (player.Length > 0 && player != _player)
            {
                _player = player;
                ReloadReferencesLocked();
            }
            if (_needsLiveReconcile)
            {
                _isReplay = LogReplayContext.IsReplayFile(log.LogName);
                _needsLiveReconcile = false;
                var entries = CombatLogStateBuilder.CurrentState.EncounterEnteredInfo;
                var latest = entries.Where(pair => pair.Key <= log.TimeStamp).OrderByDescending(pair => pair.Key).FirstOrDefault();
                var visit = EncounterSessionGrouping.VisitStart(entries, log.TimeStamp);
                if (latest.Value == null || !IsEligible(latest.Value))
                {
                    _tracker = null;
                    _ghost = null;
                    _activeBoss = null;
                    _activeBossStart = null;
                    _historicalPreview = false;
                    ReloadReferencesLocked();
                    PublishLocked();
                    return;
                }
                if (_tracker == null || _tracker.Entered != visit || _tracker.Cohort != Cohort(latest.Value))
                {
                    _tracker = new RunRaceTracker();
                    Begin(_tracker, latest.Value, visit);
                    _routeKey = "auto";
                    _automaticRoute = null;
                    _player = CombatLogStateBuilder.CurrentState.LocalPlayer?.Name ?? "";
                    _activeBoss = null;
                    _activeBossStart = null;
                    SelectGhost();
                    ReloadReferencesLocked();
                }
                Anchor(log.TimeStamp);
            }
            var wasHistorical = _historicalPreview;
            _historicalPreview = false;
            if (_tracker == null || _tracker.IsComplete || log.TimeStamp < _tracker.Entered)
            {
                if (wasHistorical) PublishLocked();
                return;
            }
            if (log.TimeStamp >= _anchorLogTime) Anchor(log.TimeStamp);
            _tracker.Advance(log.TimeStamp);
        }
    }

    private void Anchor(DateTime logTime) { _anchorLogTime = logTime; _anchorClock = Stopwatch.GetTimestamp(); }

    private void OnLiveCombat(Combat combat)
    {
        if (!CombatMonitorViewModel.IsLiveParseActive() || combat?.ParentEncounter == null) return;
        lock (_gate)
        {
            if (_disposed || _tracker == null || _tracker.IsComplete || _tracker.Cohort != Cohort(combat.ParentEncounter) ||
                EncounterSessionGrouping.VisitStart(CombatLogStateBuilder.CurrentState.EncounterEnteredInfo, combat.StartTime) != _tracker.Entered)
                return;
            var boss = combat.IsCombatWithBoss ? combat.EncounterBossDifficultyParts.Item1 : null;
            double? start = boss == null ? null : Math.Max(0, (combat.StartTime - _tracker.Entered).TotalSeconds);
            if (_activeBoss == boss && _activeBossStart == start) return;
            _activeBoss = boss;
            _activeBossStart = start;
            PublishLocked();
        }
    }

    private void OnCombatFinished(Combat combat)
    {
        if (!CombatMonitorViewModel.IsLiveParseActive() || combat?.ParentEncounter == null) return;
        RunGhost? completed = null;
        lock (_gate)
        {
            if (_disposed || _tracker == null || _tracker.Cohort != Cohort(combat.ParentEncounter)) return;
            var visit = EncounterSessionGrouping.VisitStart(CombatLogStateBuilder.CurrentState.EncounterEnteredInfo, combat.StartTime);
            if (visit != _tracker.Entered) return;
            _isReplay |= combat.IsReplay;
            _activeBoss = null;
            _activeBossStart = null;
            if (!_tracker.AddAttempt(combat.EncounterBossDifficultyParts.Item1, combat.StartTime, combat.EndTime, combat.WasBossKilled)) return;
            // Every kill is durable, but incomplete checkpoints are never PB/latest candidates.
            if (combat.WasBossKilled && !_isReplay) completed = _tracker.SavedRun(_player);
            SelectGhost();
            PublishLocked();
        }
        if (completed != null) _ = SaveAsync(new[] { completed });
    }

    private void Tick(object? state)
    {
        lock (_gate)
        {
            if (_disposed || _tracker == null || _tracker.IsComplete || _historicalPreview || !CombatMonitorViewModel.IsLiveParseActive()) return;
            _tracker.Advance(_anchorLogTime + Stopwatch.GetElapsedTime(_anchorClock));
            PublishLocked();
        }
    }

    private void OnHistoryStarted()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _tracker = null;
            _ghost = null;
            _activeBoss = null;
            _activeBossStart = null;
            ReloadReferencesLocked();
            _needsLiveReconcile = true;
            PublishLocked();
        }
    }

    /// <summary>Call after a log has been parsed, while that log's area-entry state is still available.</summary>
    public void ImportHistoricalCombats(IEnumerable<Combat> combats)
    {
        var imported = combats.ToArray();
        var isReplay = LogReplayContext.IsActive || imported.Any(combat => combat.IsReplay);
        var entries = CombatLogStateBuilder.CurrentState.EncounterEnteredInfo.OrderBy(pair => pair.Key).ToArray();
        var trackers = new Dictionary<DateTime, RunRaceTracker>();
        foreach (var combat in imported.OrderBy(combat => combat.StartTime))
        {
            if (combat.ParentEncounter == null || !IsEligible(combat.ParentEncounter)) continue;
            var entered = entries.LastOrDefault(pair => pair.Key <= combat.StartTime);
            if (entered.Key == default || entered.Value == null || Cohort(entered.Value) != Cohort(combat.ParentEncounter)) continue;
            var visit = EncounterSessionGrouping.VisitStart(entries, combat.StartTime);
            if (!trackers.TryGetValue(visit, out var tracker))
            {
                tracker = new RunRaceTracker();
                Begin(tracker, combat.ParentEncounter, visit);
                trackers[visit] = tracker;
            }
            tracker.AddAttempt(combat.EncounterBossDifficultyParts.Item1, combat.StartTime, combat.EndTime, combat.WasBossKilled);
        }
        var player = CombatLogStateBuilder.CurrentState.LocalPlayer?.Name ?? "";
        var completed = isReplay ? Array.Empty<RunGhost>() : trackers.Values.Select(tracker => tracker.SavedRun(player)).OfType<RunGhost>().ToArray();
        lock (_gate)
        {
            if (_disposed) return;
            // The same final-boss policy applies to live and historical logs.
            _tracker = trackers.OrderBy(pair => pair.Key).Select(pair => pair.Value).LastOrDefault();
            _historicalPreview = _tracker != null;
            _isReplay = isReplay;
            _needsLiveReconcile = true;
            _player = player;
            _routeKey = "auto";
            _automaticRoute = null;
            _activeBoss = null;
            _activeBossStart = null;
            SelectGhost();
            ReloadReferencesLocked();
            PublishLocked();
        }
        if (completed.Length > 0) _ = SaveAsync(completed);
    }

    private async Task SaveAsync(IEnumerable<RunGhost> runs)
    {
        await _saveWorker.WaitAsync().ConfigureAwait(false);
        try
        {
            var ghosts = await Task.Run(() => { foreach (var run in runs) _store.Save(run); return _store.Read(); }).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed) return;
                _ghosts = ghosts;
                if (_tracker == null) _snapshot = RunRaceSnapshot.Empty with { Status = $"{ghosts.Count} local ghosts available. Enter a Story operation or flashpoint to race." };
                // Keep the current race's reference stable after finishing/saving.
                else if (!_tracker.IsComplete) SelectGhost();
                PublishLocked();
            }
        }
        catch (Exception ex)
        {
            Logging.LogError("Could not save run ghost: " + ex.Message);
            lock (_gate) { if (!_disposed) PublishLocked("Run recorded for this session; local ghost storage is unavailable."); }
        }
        finally { _saveWorker.Release(); }
    }

    private void SelectGhost()
    {
        if (_tracker == null) { _ghost = null; return; }
        var remote = _online?.Routes.SelectMany(r => new[] { r.PersonalBest, r.Latest }).OfType<RunGhost>() ?? Enumerable.Empty<RunGhost>();
        var matches = _ghosts.Concat(remote).Where(_tracker.Accepts)
            .Where(run => string.Equals(run.Player, _player, StringComparison.OrdinalIgnoreCase)).ToArray();
        var observed = _tracker.SavedRun(_player);
        bool Compatible(string[] bosses) => observed == null || bosses.Take(observed.Splits.Count).SequenceEqual(observed.Splits.Select(s => s.Boss)) &&
            (!observed.IsComplete || bosses.Length == observed.Splits.Count);
        var candidates = (_online?.Routes.Where(r => Compatible(r.Bosses)).Select(r => (r.Key, Count: r.SampleCount)) ?? Enumerable.Empty<(string Key, int Count)>())
            .Concat(matches.Where(_tracker.Follows).Where(r => !_tracker.IsComplete || r.Splits.Count == observed?.Splits.Count).Select(r => (Key: r.RouteKey, Count: 1)))
            .GroupBy(r => r.Key).Select(g => (Key: g.Key, Count: g.Max(r => r.Count))).ToArray();
        if (observed?.IsComplete == true) _automaticRoute = observed.RouteKey;
        else if (_automaticRoute == null || !candidates.Any(r => r.Key == _automaticRoute))
            _automaticRoute = candidates.OrderByDescending(r => r.Count).ThenBy(r => r.Key, StringComparer.Ordinal).Select(r => r.Key).FirstOrDefault();
        var route = _routeKey == "auto" ? _automaticRoute : _routeKey;
        if (_ghostMode == LeaderboardMode) { _ghost = null; return; }
        _ghost = (_ghostMode == "Latest run" ? matches.Where(r => r.RouteKey == route).OrderByDescending(run => run.Completed)
            : matches.Where(r => r.RouteKey == route).OrderBy(run => run.DurationSeconds).ThenByDescending(run => run.Completed)).FirstOrDefault();
    }

    private void ReloadReferencesLocked()
    {
        ReloadBenchmarkLocked();
        _runGeneration++;
        _runRequest?.Cancel();
        _online = null;
        _runsLoading = false;
        _onlineStatus = _isOffline() ? "Offline · using saved local runs" : "";
        SelectGhost();
        if (_isOffline() || _tracker?.Cohort == null || string.IsNullOrWhiteSpace(_player)) { SelectGhost(); return; }
        var request = new CancellationTokenSource();
        _runRequest = request;
        _runsLoading = true;
        var generation = _runGeneration;
        var cohort = _tracker.Cohort;
        var entered = _tracker.Entered;
        var player = _player;
        var cacheReferences = !_isReplay;
        _ = Task.Run(async () =>
        {
            try
            {
                var references = await _loadRuns(cohort, player, entered, request.Token).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_disposed || request.IsCancellationRequested || generation != _runGeneration) return;
                    _online = references;
                    _runsLoading = false;
                    _onlineStatus = references == null ? "Online references unavailable · using saved local runs"
                        : !references.Ready ? "Online run history is indexing · using available runs" : "Online run history · local copies saved for offline use";
                    SelectGhost();
                    PublishLocked();
                }
                if (references != null && cacheReferences)
                    await SaveAsync(references.Routes.SelectMany(r => new[] { r.PersonalBest, r.Latest }).OfType<RunGhost>()).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                lock (_gate) if (!_disposed && generation == _runGeneration)
                { _runsLoading = false; _onlineStatus = "Online references unavailable · using saved local runs"; PublishLocked(); }
            }
            finally { lock (_gate) if (ReferenceEquals(_runRequest, request)) _runRequest = null; request.Dispose(); }
        });
    }

    private void ReloadBenchmarkLocked()
    {
        _benchmarkGeneration++;
        _benchmarkRequest?.Cancel();
        _benchmarks = new Dictionary<string, TimeTrialSummary>();
        _benchmarkLoading = false;
        if (_ghostMode != LeaderboardMode || _tracker?.Cohort == null || _isOffline()) return;
        var request = new CancellationTokenSource();
        _benchmarkRequest = request;
        _benchmarkLoading = true;
        var generation = _benchmarkGeneration;
        var cohort = _tracker.Cohort;
        var bosses = _tracker.ExpectedBosses.ToArray();
        // Fetch only when the reference or encounter changes, never from the live clock tick.
        _ = Task.Run(() => LoadBenchmarkAsync(cohort, bosses, generation, request));
    }

    private async Task LoadBenchmarkAsync(RunCohort cohort, string[] bosses, int generation, CancellationTokenSource request)
    {
        try
        {
            var values = await Task.WhenAll(bosses.Select(async boss =>
            {
                try
                {
                    var summary = await _loadBenchmark(cohort, boss, request.Token).ConfigureAwait(false);
                    return (Boss: boss, Summary: summary ?? new TimeTrialSummary());
                }
                catch (Exception error) when (error is not OperationCanceledException || !request.IsCancellationRequested)
                {
                    // A single unavailable boss does not erase the other checkpoints.
                    return (Boss: boss, Summary: new TimeTrialSummary());
                }
            })).ConfigureAwait(false);
            lock (_gate)
            {
                if (_disposed || request.IsCancellationRequested || generation != _benchmarkGeneration) return;
                _benchmarks = values.Where(v => v.Summary.SampleCount > 0 && v.Summary.StartSeconds >= 0 &&
                        v.Summary.EndSeconds > 0 && v.Summary.EndSeconds >= v.Summary.StartSeconds)
                    .ToDictionary(v => v.Boss, v => v.Summary, StringComparer.Ordinal);
                _benchmarkLoading = false;
                PublishLocked();
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_benchmarkRequest, request)) _benchmarkRequest = null;
            }
            request.Dispose();
        }
    }

    private void PublishLocked(string status = "")
    {
        var leaderboard = _ghostMode == LeaderboardMode;
        var paused = _monitoringStopped && !_historicalPreview && _tracker?.IsComplete == false;
        if (status.Length == 0 && _isReplay)
            status = _tracker?.IsComplete == true ? "Simulated run complete · no ghost saved or uploaded"
                : "Debug replay · checkpoints are simulated and will not be saved or uploaded";
        if (status.Length == 0 && paused)
            status = "Monitoring stopped · resume live parsing to continue.";
        if (status.Length == 0 && leaderboard && _tracker != null)
            status = _benchmarkLoading ? "Loading leaderboard checkpoints… Your run keeps tracking."
                : _benchmarks.Count == 0 ? "Leaderboard timings unavailable for this encounter. Your run keeps tracking."
                : $"Fastest 10% boss benchmarks · {_benchmarks.Count}/{_tracker.ExpectedBosses.Count} checkpoints · independent leaderboard timings";
        if (status.Length == 0 && !leaderboard)
            status = _runsLoading ? "Loading personal runs… Boss checkpoints keep saving locally."
                : _tracker?.IsComplete == true ? "Run complete · saved automatically. " + _onlineStatus
                : _onlineStatus;
        if (status.Length == 0 && _historicalPreview && _tracker?.IsComplete == false)
            status = "Saved checkpoints · this session did not reach the encounter's final boss.";
        _snapshot = (_tracker?.Snapshot(_ghost, _ghostMode, status) ?? RunRaceSnapshot.Empty with
        {
            GhostMode = _ghostMode,
            Status = status.Length > 0 ? status : _ghosts.Count > 0 ? $"{_ghosts.Count} local ghosts available. Enter a Story operation or flashpoint to race." : RunRaceSnapshot.Empty.Status
        }) with
        {
            RouteKey = _routeKey, Routes = Routes(), IsHistorical = _historicalPreview,
            ReferenceLabel = leaderboard ? "Leaderboard · fastest 10%" : _ghostMode,
            ReferenceDurationSeconds = leaderboard
                ? _benchmarks.Values.Select(v => (double?)v.EndSeconds).DefaultIfEmpty(null).Max()
                : _ghost?.DurationSeconds,
            IsLeaderboardReference = leaderboard, ReferenceLoading = leaderboard ? _benchmarkLoading : _runsLoading,
            ActiveBossName = _tracker?.IsComplete == false ? _activeBoss : null,
            ActiveBossStartSeconds = _tracker?.IsComplete == false ? _activeBossStart : null,
            IsPaused = paused, IsReplay = _isReplay
        };
        if (leaderboard)
        {
            _snapshot = _snapshot with { Checkpoints = _snapshot.Checkpoints.Select(checkpoint =>
                _benchmarks.TryGetValue(checkpoint.Boss, out var benchmark)
                    ? checkpoint with
                    {
                        GhostSeconds = benchmark.EndSeconds, GhostStartSeconds = benchmark.StartSeconds,
                        DeltaSeconds = checkpoint.Seconds - benchmark.EndSeconds, RouteMatches = true,
                        ReferenceSamples = benchmark.SampleCount
                    } : checkpoint).ToArray() };
        }
        var resolvedRoute = _routeKey == "auto" ? _automaticRoute : _routeKey;
        var routeBosses = _online?.Routes.FirstOrDefault(r => r.Key == resolvedRoute)?.Bosses
            ?? _ghosts.FirstOrDefault(r => r.RouteKey == resolvedRoute)?.Splits.Select(s => s.Boss).ToArray()
            ?? (_tracker?.CompletedRun(_player) is { } finished && finished.RouteKey == resolvedRoute ? finished.Splits.Select(s => s.Boss).ToArray() : null);
        if (routeBosses != null)
        {
            var included = routeBosses.Concat(_snapshot.Checkpoints.Where(c => c.Seconds.HasValue).Select(c => c.Boss)).Distinct().ToArray();
            _snapshot = _snapshot with { Checkpoints = included.Select(b => _snapshot.Checkpoints.FirstOrDefault(c => c.Boss == b)).OfType<RaceCheckpoint>().ToArray() };
            if (leaderboard) _snapshot = _snapshot with { ReferenceDurationSeconds = _snapshot.Checkpoints.Select(c => c.GhostSeconds).DefaultIfEmpty(null).Max() };
        }
        // Listeners only enqueue UI work; no UI thread waits occur under this gate.
        Updated?.Invoke(_snapshot);
    }

    private IReadOnlyList<RaceRouteOption> Routes()
    {
        var routes = (_online?.Routes.Select(r => (r.Key, r.Bosses)) ?? Enumerable.Empty<(string Key, string[] Bosses)>())
            .Concat(_ghosts.Where(r => r.IsComplete && r.Cohort == _tracker?.Cohort && string.Equals(r.Player, _player, StringComparison.OrdinalIgnoreCase))
                .Select(r => (Key: r.RouteKey, Bosses: r.Splits.Select(s => s.Boss).ToArray())))
            .Concat(_tracker?.CompletedRun(_player) is { } current ? new[] { (Key: current.RouteKey, Bosses: current.Splits.Select(s => s.Boss).ToArray()) } : Array.Empty<(string Key, string[] Bosses)>())
            .DistinctBy(r => r.Key);
        return new[] { new RaceRouteOption("auto", "Automatic · follow your boss order") }
            .Concat(routes.Select(r => new RaceRouteOption(r.Key, $"{r.Bosses.Length} bosses · {string.Join(" → ", r.Bosses)}"))).ToArray();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            CombatLogStateBuilder.AreaEntered -= OnAreaEntered;
            CombatIdentifier.CombatFinished -= OnCombatFinished;
            CombatSelectionMonitor.OnInProgressCombatSelected -= OnLiveCombat;
            CombatLogStreamer.NewLineStreamed -= OnLog;
            CombatLogStreamer.HistoricalLogsStarted -= OnHistoryStarted;
            Settings.SettingsUpdated -= OnSettingsChanged;
            _timer?.Dispose();
            _timer = null;
            _benchmarkGeneration++;
            _benchmarkRequest?.Cancel();
            _runGeneration++;
            _runRequest?.Cancel();
            Updated = null;
        }
        // Finish already queued durable writes on graceful shutdown, outside the service gate.
        _saveWorker.Wait();
        _saveWorker.Release();
    }
}
