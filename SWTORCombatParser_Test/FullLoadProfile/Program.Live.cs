using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views;

internal static partial class Program
{
    internal static bool LiveReplay;
    internal static double ReplaySpeed = 1;
    internal static DateTime? ReplayFrom, ReplayThrough;
    internal static bool PartialWrite;
    internal static bool AudioProbe;
    internal static bool FullOverlays;
    private static bool IsProfileArgument(string arg) => arg is "--no-cache" or "--audit" or "--switch" or
        "--selection-stress" or "--review-tabs" or "--progression-review" or "--historical-loading" or "--exit" or "--live-replay" or "--partial-write" or "--audio-probe" or "--full-overlays" ||
        arg.StartsWith("--replay-speed=", StringComparison.Ordinal) ||
        arg.StartsWith("--replay-from=", StringComparison.Ordinal) || arg.StartsWith("--replay-through=", StringComparison.Ordinal);

    private static void ReadLiveOptions(string[] args)
    {
        LiveReplay = args.Contains("--live-replay");
        PartialWrite = args.Contains("--partial-write");
        AudioProbe = args.Contains("--audio-probe");
        FullOverlays = args.Contains("--full-overlays");
        string? Value(string name) => args.SingleOrDefault(arg => arg.StartsWith(name + "=", StringComparison.Ordinal))?.Substring(name.Length + 1);
        if (Value("--replay-speed") is { } speed) ReplaySpeed = double.Parse(speed, CultureInfo.InvariantCulture);
        if (Value("--replay-from") is { } from) ReplayFrom = DateTime.Parse(from, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (Value("--replay-through") is { } through) ReplayThrough = DateTime.Parse(through, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        if (!double.IsFinite(ReplaySpeed) || ReplaySpeed < 0.1 || ReplaySpeed > 120) throw new ArgumentOutOfRangeException(nameof(ReplaySpeed));
        if (!LiveReplay && (PartialWrite || AudioProbe || FullOverlays || ReplaySpeed != 1 || ReplayFrom.HasValue || ReplayThrough.HasValue))
            throw new ArgumentException("Replay options require --live-replay.");
        if (LiveReplay && args.Any(arg => arg is "--no-cache" or "--selection-stress" or "--switch"))
            throw new ArgumentException("Live replay runs its own streaming selection/parity checks; omit historical stress options.");
        if (LiveReplay && !DebugFeatures.LogReplayEnabled)
            throw new InvalidOperationException("The actual supported replay path requires a Debug application build.");
        if (AudioProbe && ReplaySpeed != 1)
            throw new ArgumentException("The separately labeled audio probe requires 1x replay so native playback has real time to run.");
    }
}

public sealed partial class FullLoadApp
{
    private static object? LiveAudioInitialization;
    private static readonly JsonSerializerOptions LiveJson = new() { WriteIndented = true };
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string AudioProbeId = "90fb0639-efdc-41d4-a057-7073fe192b3a";

    private static void InitializeLiveAudio()
    {
        try
        {
            var core = Assembly.Load("LibVLCSharp").GetType("LibVLCSharp.Shared.Core", true)!;
            core.GetMethod("Initialize", new[] { typeof(string) })!.Invoke(null, new object?[] { null });
            LiveAudioInitialization = new { Initialized = true, Method = "LibVLCSharp.Shared.Core.Initialize", AudibleOutputVerified = false };
        }
        catch (Exception error)
        {
            LiveAudioInitialization = new { Initialized = false, Error = error.ToString(), AudibleOutputVerified = false };
            Program.HadFailure = true;
        }
    }

    private sealed record ReplaySource(DateTime Start, DateTime PlaybackStart, DateTime End, int PreludeLines,
        DateTime[] OutputTimes, string PayloadHash, string LivePayloadHash, string SourceFileHash);
    private sealed record LiveParticipant(long LogId, string Name, double Damage, double Healing, double EffectiveHealing,
        double Shielding, double Threat, double DamageTaken, double Cooldown);
    private sealed record LiveCombat(DateTime Start, DateTime End, string Boss, bool Killed, int LogCount,
        string LogFingerprint, LiveParticipant[] Participants);
    private const double LiveAbsoluteTolerance = 1e-7;
    private const double LiveRelativeTolerance = 1e-12;
    private sealed record NumericDeviation(long ParticipantLogId, string ParticipantName, string Metric,
        double Live, double Historical, double AbsoluteDifference, double RelativeDifference, bool WithinTolerance);
    private sealed record LiveComparison(bool Matches, bool ExactStructureMatches, int NumericFieldsCompared,
        double MaximumAbsoluteDifference, double MaximumRelativeDifference, NumericDeviation[] Deviations);
    private sealed record ReplayTimerStop(int ControllerEntries, int DefinitionInstanceEntries, int ObservedTriggeredInstances,
        object[] ControllerTimers, object[] DefinitionTimers, object[] ObservedTriggeredTimers);

    private sealed class LiveObservations
    {
        internal readonly Stopwatch Clock = Stopwatch.StartNew();
        internal readonly object Gate = new();
        internal readonly HashSet<long> Lines = new();
        internal readonly ConcurrentQueue<object> Timeline = new();
        internal readonly ConcurrentQueue<object> Samples = new();
        internal readonly ConcurrentQueue<string> Errors = new();
        internal readonly ConcurrentDictionary<DateTime, LiveCombat> Completed = new();
        internal readonly ConcurrentQueue<double> HeartbeatDelays = new();
        internal long LineEvents, DuplicateLines, OutOfOrderLines, LastLine = -1;
        internal TimeSpan? RebaseOffset;
        internal bool MonitoringStarted;
        internal bool PreludeFinished;
        internal bool StreamingFinished;
        internal DateTime? LatestRequestedStart;
        internal int ReviewClicks, CurrentClicks, ExplicitPublications, StalePublications;
        internal int InitialPublications, PublishedReviewSelections, PublishedCurrentSelections, SuccessfulPairs;
        internal TaskCompletionSource<bool>? SelectionWaiter;
        internal DateTime SelectionLiveStart;
        internal void Error(Exception error) => Errors.Enqueue(error.ToString());
    }

    private static async Task RunLiveReplay(MainWindow window, MainWindowViewModel vm)
    {
        var monitor = (CombatMonitorViewModel)typeof(MainWindowViewModel).GetField("_combatMonitorViewModel", PrivateInstance)!.GetValue(vm)!;
        var streamer = (CombatLogStreamer)typeof(CombatMonitorViewModel).GetField("_combatLogStreamer", PrivateInstance)!.GetValue(monitor)!;
        var state = new LiveObservations();
        ReplaySource? source = null;
        object? lineReport = null, parity = null, finalResources = null;
        ReplayTimerStop? timerStop = null;
        string? failure = null;
        var copiedLog = Path.Combine(Program.OutputDirectory, "replayed-log.txt");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(45));
        using var samplingStop = new CancellationTokenSource();
        using var timers = new LiveTimerObserver(state);
        Task heartbeat = Task.CompletedTask, selections = Task.CompletedTask;
        var stopCount = 0;
        var originalLogPath = Program.LogPath;
        Action<DateTime, bool> onPrelude = (_, _) => { if (LogReplayContext.IsActive) state.PreludeFinished = true; };
        Action<ParsedLogEntry> onLine = line =>
        {
            try
            {
                if (!LogReplayContext.IsActive || source == null) return;
                lock (state.Gate)
                {
                    state.LineEvents++;
                    if (!state.Lines.Add(line.LogLineNumber)) state.DuplicateLines++;
                    if (line.LogLineNumber <= state.LastLine) state.OutOfOrderLines++;
                    state.LastLine = line.LogLineNumber;
                    if (!state.RebaseOffset.HasValue && line.LogLineNumber >= 0 && line.LogLineNumber < source.OutputTimes.Length)
                        state.RebaseOffset = line.TimeStamp - source.OutputTimes[(int)line.LogLineNumber];
                }
            }
            catch (Exception error) { state.Error(error); }
        };
        Action<CombatStatusUpdate> onCombat = update =>
        {
            try
            {
                // CombatStatusUpdate.IsRealtime is not populated by the streamer.
                // The historical-ready event marks the actual live handoff instead.
                if (!LogReplayContext.IsActive || !state.PreludeFinished || state.StreamingFinished) return;
                if (update.Type is UpdateType.Start or UpdateType.Stop)
                    lock (state.Gate)
                        if (state.SelectionWaiter != null &&
                            (update.Type == UpdateType.Stop && update.CombatStartTime == state.SelectionLiveStart ||
                             update.Type == UpdateType.Start && update.CombatStartTime != state.SelectionLiveStart))
                            state.SelectionWaiter.TrySetResult(false);
                if (update.Type != UpdateType.Update)
                    state.Timeline.Enqueue(new { Kind = "Combat" + update.Type, WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds, update.CombatStartTime, Lines = update.Logs?.Count });
                if (update.Type == UpdateType.Stop)
                {
                    Interlocked.Increment(ref stopCount);
                    // The monitor's earlier subscriber has already completed this
                    // combat. Capture delivered values before any disk reload can rebuild them.
                    var delivered = CombatIdentifier.CurrentCombat;
                    if (delivered.StartTime != update.CombatStartTime)
                        throw new InvalidOperationException("A stop event did not leave its completed combat available for live metric verification.");
                    state.Completed[delivered.StartTime] = CaptureLiveCombat(delivered);
                }
            }
            catch (Exception error) { state.Error(error); }
        };
        Action<bool> onMonitoring = active =>
        {
            try
            {
                if (active) state.MonitoringStarted = true;
                else if (state.MonitoringStarted && LogReplayContext.IsActive)
                {
                    state.StreamingFinished = true;
                    File.Copy(streamer.CurrentLog, copiedLog, false);
                    if (source != null && state.RebaseOffset.HasValue)
                        File.SetLastWriteTime(copiedLog, source.Start + state.RebaseOffset.Value);
                    state.Timeline.Enqueue(new { Kind = "StreamingStopped", WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds });
                }
            }
            catch (Exception error) { state.Error(error); }
        };
        Action<Combat, bool> onSelection = (combat, current) =>
        {
            if (!state.LatestRequestedStart.HasValue)
            {
                state.InitialPublications++;
                state.Timeline.Enqueue(new { Kind = "InitialSelectionPublished", WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds,
                    combat.StartTime, IsCurrent = current });
                return;
            }
            var matches = state.LatestRequestedStart == combat.StartTime;
            state.ExplicitPublications++;
            if (!matches) state.StalePublications++;
            else
            {
                if (current) state.PublishedCurrentSelections++; else state.PublishedReviewSelections++;
                lock (state.Gate) state.SelectionWaiter?.TrySetResult(true);
            }
            state.Timeline.Enqueue(new { Kind = "ExplicitSelectionPublished", WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds,
                combat.StartTime, IsCurrent = current, MatchesLatestRequest = matches, Phases = CaptureLivePhases() });
        };
        try
        {
            source = await Task.Run(() => InspectReplaySource(Program.LogPath), cancellation.Token);
            await ConfigureLiveOverlays(vm, cancellation.Token);
            CombatLogStreamer.NewLineStreamed += onLine;
            CombatLogStreamer.HistoricalLogsFinished += onPrelude;
            CombatLogStreamer.CombatUpdated += onCombat;
            monitor.OnMonitoringStateChanged += onMonitoring;
            CombatSelectionMonitor.SelectedCombatChanged += onSelection;
            timers.Subscribe();
            heartbeat = SampleLiveUi(vm, monitor, state, timers, samplingStop.Token);
            selections = ExerciseLiveSelections(vm, monitor, state, samplingStop.Token);
            var request = new LogReplayRequest(Program.LogPath, Program.ReplayFrom, Program.ReplayThrough,
                Program.PartialWrite, Program.ReplaySpeed);
            typeof(CombatMonitorViewModel).GetProperty(nameof(CombatMonitorViewModel.IsReplaying))!.SetValue(monitor, true);
            typeof(CombatMonitorViewModel).GetField("_replayCancellation", PrivateInstance)!.SetValue(monitor, cancellation);
            var replayTask = (Task)typeof(CombatMonitorViewModel).GetMethod("RunReplayAsync", PrivateInstance)!
                .Invoke(monitor, new object[] { request, cancellation.Token })!;
            typeof(CombatMonitorViewModel).GetField("_replayTask", PrivateInstance)!.SetValue(monitor, replayTask);
            await replayTask.WaitAsync(cancellation.Token);
            if (!monitor.ReplayStatus.StartsWith("Replay complete", StringComparison.Ordinal))
                throw new InvalidOperationException(monitor.ReplayStatus);
            await WaitForMeasuredWorkAndIdle(cancellation.Token);
            samplingStop.Cancel();
            await Task.WhenAll(heartbeat, selections);
            timerStop = await Dispatcher.UIThread.InvokeAsync(timers.CaptureStopState);
            if (timerStop.ControllerEntries != 0 || timerStop.DefinitionInstanceEntries != 0 || timerStop.ObservedTriggeredInstances != 0)
                throw new InvalidOperationException("Replay finished with active timer entries or observed timer instances still triggered; inspect TimerCleanup.");
            CombatLogStreamer.NewLineStreamed -= onLine;
            CombatLogStreamer.HistoricalLogsFinished -= onPrelude;
            CombatLogStreamer.CombatUpdated -= onCombat;
            monitor.OnMonitoringStateChanged -= onMonitoring;
            CombatSelectionMonitor.SelectedCombatChanged -= onSelection;
            timers.Dispose();
            LogLoadDiagnostics.WriteReport("Live replay finished and measured UI work settled");
            if (File.Exists(Path.Combine(Program.OutputDirectory, "stage-report.json")))
                File.Copy(Path.Combine(Program.OutputDirectory, "stage-report.json"), Path.Combine(Program.OutputDirectory, "live-stage-report.json"));
            lineReport = ValidateReplayLines(copiedLog, source, state,
                (int)typeof(CombatLogStreamer).GetField("numberOfProcessedLines", PrivateInstance)!.GetValue(streamer)!);
            if (state.DuplicateLines != 0 || state.OutOfOrderLines != 0 || state.StalePublications != 0)
                throw new InvalidOperationException("Replay produced duplicate/out-of-order streamed lines or an obsolete explicit selection publication.");
            if (!state.Errors.IsEmpty) throw new InvalidOperationException("A live observation failed; see Errors in live-replay.json.");
            timers.ValidateAudioProbe();
            var afterLive = await Dispatcher.UIThread.InvokeAsync(() => CaptureLiveUi(vm, monitor));
            await CollectOutsideLatency();
            finalResources = new { Resources = CaptureResource(Array.Empty<PastCombat>(), state.Clock.Elapsed.TotalMilliseconds), Ui = afterLive };
            // Reparse the exact generated bytes using the full normal historical
            // pipeline, after live snapshots have become scalar records.
            var expected = new Dictionary<DateTime, LiveCombat>();
            Action<List<Combat>> reference = combats =>
            {
                foreach (var combat in combats) expected[combat.StartTime] = CaptureLiveCombat(combat);
            };
            monitor.OnHistoricalCombatsParsed += reference;
            try
            {
                Program.LogPath = copiedLog;
                if (!await Load(window, vm)) throw new InvalidOperationException("Historical reference load failed.");
            }
            finally { monitor.OnHistoricalCombatsParsed -= reference; }
            var incompleteTailCompared = false;
            if (CombatLogStreamer.InCombat)
            {
                var tail = (List<ParsedLogEntry>)typeof(CombatLogStreamer).GetField("_currentCombatLogs", PrivateInstance)!.GetValue(streamer)!;
                if (tail.Count > 0)
                {
                    // Ending replay flushes an unfinished combat. The historical
                    // loader leaves that same tail open, so compare a snapshot of
                    // those exact parsed tail records explicitly.
                    var tailCombat = CombatIdentifier.GenerateCombatSnapshotFromLogs(tail.ToList(), combatEndUpdate: true);
                    expected[tailCombat.StartTime] = CaptureLiveCombat(tailCombat);
                    incompleteTailCompared = true;
                }
            }
            var differences = state.Completed.Values.OrderBy(combat => combat.Start).Select(actual =>
            {
                expected.TryGetValue(actual.Start, out var historical);
                var comparison = CompareLiveCombat(actual, historical);
                return new { Start = actual.Start, actual.Boss, Matches = comparison.Matches, Comparison = comparison,
                    Live = actual, Historical = historical };
            }).ToArray();
            var replayStart = source.PlaybackStart + (state.RebaseOffset ?? TimeSpan.Zero);
            var missingLive = expected.Values.Where(combat => combat.Start >= replayStart && !state.Completed.ContainsKey(combat.Start))
                .Select(combat => new { combat.Start, combat.End, combat.Boss }).ToArray();
            parity = new { DeliveredLiveCombats = state.Completed.Count, HistoricalCombats = expected.Count, Compared = differences.Length,
                IncompleteTailCompared = incompleteTailCompared,
                NumericTolerance = new { Absolute = LiveAbsoluteTolerance, Relative = LiveRelativeTolerance,
                    Rule = "Numeric values pass only if finite and absolute difference <= max(absolute tolerance, relative tolerance * max(abs(live), abs(historical))). All structure, times, identities and log fingerprints remain exact." },
                MaximumAbsoluteNumericDifference = differences.Select(comparison => comparison.Comparison.MaximumAbsoluteDifference).DefaultIfEmpty().Max(),
                MaximumRelativeNumericDifference = differences.Select(comparison => comparison.Comparison.MaximumRelativeDifference).DefaultIfEmpty().Max(),
                Mismatches = differences.Where(comparison => !comparison.Matches).ToArray(), MissingLiveCompletions = missingLive,
                MatchingCombats = differences.Where(comparison => comparison.Matches).Select(comparison => new { comparison.Start, comparison.Boss,
                    NumericComparison = comparison.Comparison }).ToArray() };
            if (differences.Length == 0 || differences.Any(comparison => !comparison.Matches) || missingLive.Length != 0)
                throw new InvalidOperationException("Delivered live combat metrics differ from the historical parse of the exact replay output; inspect parity details.");
        }
        catch (Exception error) { failure = error.ToString(); Program.HadFailure = true; }
        finally
        {
            cancellation.Cancel();
            samplingStop.Cancel();
            try { await Task.WhenAll(heartbeat, selections); } catch (Exception error) { state.Error(error); }
            CombatLogStreamer.NewLineStreamed -= onLine;
            CombatLogStreamer.HistoricalLogsFinished -= onPrelude;
            CombatLogStreamer.CombatUpdated -= onCombat;
            monitor.OnMonitoringStateChanged -= onMonitoring;
            CombatSelectionMonitor.SelectedCombatChanged -= onSelection;
            var cancellationField = typeof(CombatMonitorViewModel).GetField("_replayCancellation", PrivateInstance)!;
            if (ReferenceEquals(cancellationField.GetValue(monitor), cancellation)) cancellationField.SetValue(monitor, null);
            if (!state.Errors.IsEmpty) Program.HadFailure = true;
            var delays = state.HeartbeatDelays.Order().ToArray();
            WriteLive("live-replay-timeline.json", state.Timeline.ToArray());
            WriteLive("live-replay-samples.json", state.Samples.ToArray());
            WriteLive("live-delivered-combats.json", state.Completed.Values.OrderBy(combat => combat.Start).ToArray());
            WriteLive("live-replay.json", new
            {
                Status = failure == null && !Program.HadFailure ? "Passed" : "Failed", Error = failure, Errors = state.Errors.ToArray(),
                Program.ReplaySpeed, Program.ReplayFrom, Program.ReplayThrough, Program.PartialWrite,
                SourceLog = originalLogPath, ReplayedLog = copiedLog,
                Source = source == null ? null : new { source.Start, source.PlaybackStart, source.End, source.PreludeLines,
                    OutputLines = source.OutputTimes.Length, source.SourceFileHash },
                Audio = LiveAudioInitialization, TimerObservations = timers.Summary(), Lines = lineReport, Parity = parity,
                TimerCleanup = timerStop,
                Program.FullOverlays,
                AudioProbe = Program.AudioProbe ? new { Id = AudioProbeId, Label = "PROFILE ONLY - native audio probe",
                    Trigger = "First recognized boss combat; six-second countdown, audio requested with five seconds remaining",
                    Expected = "At least one trigger, request flag and native Playing event; no native media error", ChangesUserDefaults = false } : null,
                Selection = new { state.ReviewClicks, state.CurrentClicks, TargetPairs = 5, state.ExplicitPublications, state.StalePublications,
                    state.InitialPublications, state.PublishedReviewSelections, state.PublishedCurrentSelections, state.SuccessfulPairs,
                    Coverage = state.SuccessfulPairs >= 5 ? "Five published review/current pairs exercised" : "Insufficient eligible overlapping prior/current rows during this replay window" },
                UiHeartbeat = new { Samples = delays.Length, MaximumDispatchMilliseconds = delays.DefaultIfEmpty().Max(),
                    P95DispatchMilliseconds = delays.Length == 0 ? 0 : delays[(int)((delays.Length - 1) * .95)] },
                StopEvents = stopCount, FinalResources = finalResources,
                Notes = "Debug build, actual native UI and disk-tailing replay. The normal 250 ms parser polling interval is unchanged; accelerated replay intentionally produces larger batches. Raw payload checks exclude the deliberately rebased 14-character clock prefix and normalize line endings. Audio flags/native media events do not verify sound from physical speakers, nor real-time audio deadlines at accelerated speed. Live snapshots are captured at Stop before cache reload; parity differences are not automatically tolerated. Resource sampling and scalar combat fingerprints add diagnostic overhead. A short window may have no completed fight or insufficient prior/current selections; inspect coverage."
            });
        }
    }

    private static void WriteLive(string name, object value) => File.WriteAllText(Path.Combine(Program.OutputDirectory, name), JsonSerializer.Serialize(value, LiveJson));

    private static void ScheduleLiveFinalMemory(MainWindowViewModel vm)
    {
        // The posted callback captures only the app's existing VM. RunLiveReplay,
        // its line-index set and scalar sample queues have returned before this GC.
        Dispatcher.UIThread.Post(async () =>
        {
            var elapsed = Stopwatch.StartNew();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                await WaitForMeasuredWorkAndIdle(timeout.Token);
                await CollectOutsideLatency();
                var monitor = (CombatMonitorViewModel)typeof(MainWindowViewModel).GetField("_combatMonitorViewModel", PrivateInstance)!.GetValue(vm)!;
                var counts = await Dispatcher.UIThread.InvokeAsync(() => (
                    Rows: monitor.PastEncounters.Sum(encounter => encounter.EncounterCombats.Count),
                    Resident: monitor.PastEncounters.Sum(encounter => encounter.EncounterCombats.Count(row => row.Combat != null))));
                var resources = CaptureResource(Array.Empty<PastCombat>(), elapsed.Elapsed.TotalMilliseconds) with { ResidentCombats = counts.Resident };
                var ui = await Dispatcher.UIThread.InvokeAsync(() => CaptureLiveUi(vm, monitor));
                WriteLive("live-final-memory.json", new { Status = "Completed", Resources = resources, counts.Rows, Ui = ui,
                    Note = "Forced-GC sample after the entire live probe returns and releases its diagnostic queues. A successful run has also loaded the saved replay output as its historical parity reference; this describes that final UI state, not the earlier live-stop state. See live-replay.FinalResources for the live-stop snapshot." });
            }
            catch (Exception error)
            {
                Program.HadFailure = true;
                File.WriteAllText(Path.Combine(Program.OutputDirectory, "live-final-memory-error.txt"), error.ToString());
            }
            finally { Program.Complete(); }
        });
    }

    private static ReplaySource InspectReplaySource(string path)
    {
        using var file = File.OpenRead(path);
        var sourceHash = Convert.ToHexString(SHA256.HashData(file));
        file.Position = 0;
        using var reader = new StreamReader(file, Encoding.GetEncoding(1252), true);
        var name = Path.GetFileName(path);
        var fileDate = name.StartsWith("combat_", StringComparison.OrdinalIgnoreCase) && name.Length >= 26 &&
            DateTime.TryParseExact(name.AsSpan(7, 19), "yyyy-MM-dd_HH_mm_ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed : File.GetLastWriteTime(path).Date;
        var date = fileDate.Date;
        DateTime? latest = null, playback = null;
        var times = new List<DateTime>();
        var prelude = 0;
        using var all = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var live = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!TryClock(line, out var clock)) continue;
            var timestamp = date + clock;
            if (timestamp < (latest ?? fileDate).AddHours(-12)) { date = date.AddDays(1); timestamp = date + clock; }
            if (!latest.HasValue || timestamp > latest) latest = timestamp;
            if (Program.ReplayThrough.HasValue && timestamp > Program.ReplayThrough.Value) break;
            if (!playback.HasValue && (!Program.ReplayFrom.HasValue || timestamp >= Program.ReplayFrom.Value)) playback = timestamp;
            if (playback.HasValue && timestamp < playback.Value) continue;
            times.Add(timestamp);
            AppendPayload(all, line);
            if (playback.HasValue) AppendPayload(live, line); else prelude++;
        }
        if (!playback.HasValue || times.Count == 0) throw new InvalidDataException("No timestamped events in the requested replay window.");
        return new ReplaySource(times[0], playback.Value, times.Max(), prelude, times.ToArray(),
            Convert.ToHexString(all.GetHashAndReset()), Convert.ToHexString(live.GetHashAndReset()), sourceHash);
    }

    private static bool TryClock(string line, out TimeSpan clock)
    {
        clock = default;
        return line.Length >= 14 && line[0] == '[' && line[13] == ']' &&
            TimeSpan.TryParseExact(line.AsSpan(1, 12), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out clock);
    }
    private static void AppendPayload(IncrementalHash hash, string line) => hash.AppendData(Encoding.UTF8.GetBytes(line[14..] + "\n"));

    private static object ValidateReplayLines(string copiedLog, ReplaySource source, LiveObservations state, int consumed)
    {
        if (!File.Exists(copiedLog)) throw new FileNotFoundException("The replay output was not preserved before disposal.", copiedLog);
        using var all = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var live = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var lines = 0;
        var missing = new List<long>();
        foreach (var line in File.ReadLines(copiedLog, Encoding.UTF8))
        {
            if (!TryClock(line, out _)) throw new InvalidDataException("Replay output contains a non-timestamped line.");
            AppendPayload(all, line);
            if (state.Lines.Contains(lines)) AppendPayload(live, line);
            else if (lines >= source.PreludeLines) missing.Add(lines);
            lines++;
        }
        var actual = Convert.ToHexString(all.GetHashAndReset());
        var streamed = Convert.ToHexString(live.GetHashAndReset());
        using var original = File.OpenRead(Program.LogPath);
        var unchanged = source.SourceFileHash == Convert.ToHexString(SHA256.HashData(original));
        var report = new { ExpectedOutputLines = source.OutputTimes.Length, OutputLines = lines, ParserConsumedLines = consumed,
            OriginalSourceUnchanged = unchanged,
            ExpectedLiveLines = source.OutputTimes.Length - source.PreludeLines, StreamedEvents = state.LineEvents,
            UniqueStreamedLines = state.Lines.Count, state.DuplicateLines, state.OutOfOrderLines, MissingStreamedLines = missing.Count,
            MissingLineIndices = missing.Take(100).ToArray(), SourcePayloadSHA256 = source.PayloadHash, OutputPayloadSHA256 = actual,
            ExpectedLivePayloadSHA256 = source.LivePayloadHash, ObservedLivePayloadSHA256 = streamed,
            PayloadsMatch = actual == source.PayloadHash && streamed == source.LivePayloadHash };
        WriteLive("live-line-verification.json", report);
        if (!unchanged || lines != source.OutputTimes.Length || consumed != lines || missing.Count != 0 || actual != source.PayloadHash || streamed != source.LivePayloadHash)
            throw new InvalidOperationException("Replay raw payload or parser line coverage differs; inspect live-line-verification.json.");
        return report;
    }

    private static LiveCombat CaptureLiveCombat(Combat combat)
    {
        var damage = combat.TotalDamage;
        var participants = combat.AllEntities.Values.OrderBy(entity => entity.LogId).ThenBy(entity => entity.Name, StringComparer.Ordinal)
            .Select(entity => new LiveParticipant(entity.LogId, entity.Name, damage.GetValueOrDefault(entity),
                combat.TotalHealing.GetValueOrDefault(entity), combat.TotalEffectiveHealing.GetValueOrDefault(entity),
                combat.TotalProvidedSheilding.GetValueOrDefault(entity), combat.TotalThreat.GetValueOrDefault(entity),
                combat.TotalDamageTaken.GetValueOrDefault(entity), combat.AverageDamageSavedDuringCooldown.GetValueOrDefault(entity))).ToArray();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var line in combat.AllLogs.OrderBy(pair => pair.Key).Select(pair => pair.Value))
            hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(new { line.LogLineNumber, line.TimeStamp,
                Source = line.Source.LogId, Target = line.Target.LogId, line.AbilityId, line.Effect.EffectId, line.Effect.EffectType,
                Value = line.Value?.DblValue, Effective = line.Value?.EffectiveDblValue, line.Threat }));
        return new LiveCombat(combat.StartTime, combat.EndTime, combat.EncounterBossInfo, combat.WasBossKilled,
            combat.AllLogs.Count, Convert.ToHexString(hash.GetHashAndReset()), participants);
    }

    private static LiveComparison CompareLiveCombat(LiveCombat actual, LiveCombat? expected)
    {
        if (expected == null) return new LiveComparison(false, false, 0, 0, 0, Array.Empty<NumericDeviation>());
        var structure = actual.Start == expected.Start && actual.End == expected.End && actual.Boss == expected.Boss &&
            actual.Killed == expected.Killed && actual.LogCount == expected.LogCount && actual.LogFingerprint == expected.LogFingerprint &&
            actual.Participants.Select(participant => (participant.LogId, participant.Name))
                .SequenceEqual(expected.Participants.Select(participant => (participant.LogId, participant.Name)));
        var deviations = new List<NumericDeviation>();
        var fields = 0;
        var numericMatches = true;
        foreach (var participant in actual.Participants)
        {
            var reference = expected.Participants.SingleOrDefault(candidate => candidate.LogId == participant.LogId && candidate.Name == participant.Name);
            if (reference == null) { numericMatches = false; continue; }
            Check(nameof(participant.Damage), participant.Damage, reference.Damage);
            Check(nameof(participant.Healing), participant.Healing, reference.Healing);
            Check(nameof(participant.EffectiveHealing), participant.EffectiveHealing, reference.EffectiveHealing);
            Check(nameof(participant.Shielding), participant.Shielding, reference.Shielding);
            Check(nameof(participant.Threat), participant.Threat, reference.Threat);
            Check(nameof(participant.DamageTaken), participant.DamageTaken, reference.DamageTaken);
            Check(nameof(participant.Cooldown), participant.Cooldown, reference.Cooldown);

            void Check(string metric, double live, double historical)
            {
                fields++;
                if (live == historical && double.IsFinite(live)) return;
                var difference = Math.Abs(live - historical);
                var scale = Math.Max(Math.Abs(live), Math.Abs(historical));
                var relative = scale == 0 ? 0 : difference / scale;
                var within = double.IsFinite(live) && double.IsFinite(historical) &&
                    difference <= Math.Max(LiveAbsoluteTolerance, LiveRelativeTolerance * scale);
                deviations.Add(new NumericDeviation(participant.LogId, participant.Name, metric,
                    live, historical, difference, relative, within));
                numericMatches &= within;
            }
        }
        return new LiveComparison(structure && numericMatches, structure, fields,
            deviations.Select(deviation => deviation.AbsoluteDifference).DefaultIfEmpty().Max(),
            deviations.Select(deviation => deviation.RelativeDifference).DefaultIfEmpty().Max(), deviations.ToArray());
    }

    private static async Task ConfigureLiveOverlays(MainWindowViewModel vm, CancellationToken token)
    {
        var overlay = typeof(MainWindowViewModel).GetField("_overlayViewModel", PrivateInstance)!.GetValue(vm)!;
        var timers = (TimersCreationViewModel)overlay.GetType().GetField("_timersViewModel")!.GetValue(overlay)!;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            timers.EncounterTimersActive = true;
            timers.DisciplineTimersActive = true;
            timers.AlertsActive = true;
            ChallengeSetupViewModel._challengeWindowViewModel.Active = true;
            if (Program.FullOverlays) ConfigureFullOverlayOptions(vm);
        });
        if (Program.AudioProbe)
        {
            // This is a separately requested test condition, never part of the
            // default stock-timer performance run and never saved to user settings.
            DefaultOrbsTimersManager.AddSource(new DefaultTimersData
            {
                TimerSource = "Shared", Timers = new List<SWTORCombatParser.DataStructures.Timer>
                {
                    new()
                    {
                        Id = AudioProbeId, Name = "PROFILE ONLY - native audio probe", TimerSource = "Shared",
                        IsEnabled = true, IsUserAddedTimer = true, SpecificEncounter = "All", SpecificDifficulty = "All",
                        TriggerType = TimerKeyType.CombatStart, DurationSec = 6, AudioStartTime = 5, UseAudio = true,
                        DontRefresh = true, TimerColor = Avalonia.Media.Colors.DeepSkyBlue
                    }
                }
            });
        }
        // The real timer creation viewmodel installs shipped definitions asynchronously.
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action initialized = () => ready.TrySetResult();
        TimerController.TimersInitialized += initialized;
        try
        {
            TimerController.RefreshAvailableTimers(true);
            await ready.Task.WaitAsync(TimeSpan.FromMinutes(2), token);
            if (Program.AudioProbe)
            {
                var installed = DefaultOrbsTimersManager.GetAllDefaults().SelectMany(source => source.Timers)
                    .Count(timer => timer.Id == AudioProbeId && timer.IsEnabled);
                var available = ((IEnumerable<TimerInstance>)typeof(TimerController)
                    .GetField("_availableTimers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!)
                    .Count(timer => timer.SourceTimer.Id == AudioProbeId && timer.IsEnabled);
                WriteLive("audio-probe-setup.json", new { Id = AudioProbeId, Source = "Shared", EnabledDefinitions = installed,
                    ActiveTimerDefinitions = available, IsolatedSettingsDirectory = Path.Combine(Program.OutputDirectory, "settings") });
                if (installed != 1 || available != 1)
                    throw new InvalidOperationException("The diagnostic audio timer was not retained by the actual default-source filter and timer refresh.");
            }
        }
        finally { TimerController.TimersInitialized -= initialized; }
    }

    private static object CaptureLiveUi(MainWindowViewModel vm, CombatMonitorViewModel monitor) => new
    {
        DisplayedStart = vm.CurrentlyDisplayedCombat?.StartTime, monitor.IsReplaying, monitor.ReplayStatus, InCombat = CombatLogStreamer.InCombat,
        CurrentCombatStart = CombatIdentifier.CurrentCombat?.StartTime, Phases = CaptureLivePhases(),
        OverlayWindows = CaptureOverlayWindows(),
        Rows = monitor.PastEncounters.Sum(encounter => encounter.EncounterCombats.Count),
        ResidentRows = monitor.PastEncounters.Sum(encounter => encounter.EncounterCombats.Count(row => row.Combat != null)),
        ActiveTimers = TimerController.GetActiveTimers().Values.Select(timer => new { timer.SourceTimer.Id, timer.TimerName,
            timer.TimerValue, timer.SourceTimer.TrackOutsideOfCombat, timer.SourceTimer.UseAudio }).ToArray(),
        ActiveChallenges = ChallengeSetupViewModel._challengeWindowViewModel.ActiveChallengeInstances.Select(challenge => challenge.SourceChallengeId).ToArray(),
        VisibleOverlayWindows = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.Count(window => window is BaseOverlayWindow && window.IsVisible) : 0,
        OverlayWindowObjects = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime lifetime
            ? lifetime.Windows.Count(window => window is BaseOverlayWindow) : 0
    };

    private static object CaptureLivePhases() => new
    {
        Live = PhaseManager.GetLivePhaseSnapshot().Select(phase => new { SourceId = phase.SourcePhase.Id, phase.SourcePhase.Name, phase.PhaseStart, phase.PhaseEnd }).ToArray(),
        Displayed = PhaseManager.GetDisplayedPhaseSnapshot().Select(phase => new { SourceId = phase.SourcePhase.Id, phase.SourcePhase.Name, phase.PhaseStart, phase.PhaseEnd }).ToArray()
    };

    private static async Task SampleLiveUi(MainWindowViewModel vm, CombatMonitorViewModel monitor, LiveObservations state, LiveTimerObserver timers, CancellationToken token)
    {
        var lastProgressMilliseconds = 0d;
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
                var sent = Stopwatch.GetTimestamp();
                var snapshot = await Dispatcher.UIThread.InvokeAsync(() => CaptureLiveUi(vm, monitor), DispatcherPriority.Normal).GetTask().WaitAsync(token);
                var delay = Stopwatch.GetElapsedTime(sent).TotalMilliseconds;
                state.HeartbeatDelays.Enqueue(delay);
                state.Samples.Enqueue(new { WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds, UiDispatchMilliseconds = delay,
                    Resources = CaptureResource(Array.Empty<PastCombat>(), state.Clock.Elapsed.TotalMilliseconds), Ui = snapshot });
                if (state.Clock.Elapsed.TotalMilliseconds - lastProgressMilliseconds >= 5000)
                {
                    lastProgressMilliseconds = state.Clock.Elapsed.TotalMilliseconds;
                    var path = Path.Combine(Program.OutputDirectory, "live-progress.json");
                    File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new { WallMilliseconds = lastProgressMilliseconds,
                        StreamedLines = Interlocked.Read(ref state.LineEvents), LastLineIndex = Interlocked.Read(ref state.LastLine),
                        CompletedCombats = state.Completed.Count, state.ReviewClicks, state.CurrentClicks, state.SuccessfulPairs,
                        TimerObservations = timers.Summary(), Ui = snapshot }, LiveJson));
                    File.Move(path + ".tmp", path, true);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { state.Error(error); }
    }

    private static async Task ExerciseLiveSelections(MainWindowViewModel vm, CombatMonitorViewModel monitor, LiveObservations state, CancellationToken token)
    {
        try
        {
            var nextCurrent = false;
            DateTime reviewedLiveStart = default;
            while (!token.IsCancellationRequested && state.SuccessfulPairs < 5)
            {
                await Task.Delay(250, token);
                var attempt = await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!monitor.IsReplaying || !state.PreludeFinished || !CombatLogStreamer.InCombat) return null;
                    var rows = monitor.PastEncounters.SelectMany(encounter => encounter.EncounterCombats).ToArray();
                    var current = rows.FirstOrDefault(row => row.IsCurrentCombat && row.Combat != null);
                    var prior = rows.FirstOrDefault(row => !row.IsCurrentCombat && row.CombatStartTime < current?.CombatStartTime);
                    if (nextCurrent && current?.CombatStartTime != reviewedLiveStart)
                    {
                        nextCurrent = false;
                        return null;
                    }
                    var row = nextCurrent ? current : prior;
                    if (row == null || current == null) return null;
                    var signal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (state.Gate)
                    {
                        state.SelectionWaiter = signal;
                        state.SelectionLiveStart = current.CombatStartTime;
                    }
                    state.LatestRequestedStart = row.CombatStartTime;
                    state.Timeline.Enqueue(new { Kind = "SelectionClick", WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds,
                        row.CombatStartTime, row.IsCurrentCombat, PreviouslyDisplayed = vm.CurrentlyDisplayedCombat?.StartTime });
                    row.SelectionToggle();
                    if (nextCurrent) state.CurrentClicks++; else state.ReviewClicks++;
                    return new LiveSelectionAttempt(row.CombatStartTime, current.CombatStartTime, nextCurrent, signal.Task);
                }).GetTask().WaitAsync(token);
                if (attempt == null) continue;
                bool published;
                try { published = await attempt.Published.WaitAsync(TimeSpan.FromSeconds(10), token); }
                catch (TimeoutException)
                {
                    state.Timeline.Enqueue(new { Kind = "SelectionPublicationTimeout", attempt.Start, attempt.IsCurrent,
                        WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds });
                    nextCurrent = false;
                    continue;
                }
                if (!published) { nextCurrent = false; continue; }
                if (attempt.IsCurrent)
                {
                    state.SuccessfulPairs++;
                    nextCurrent = false;
                }
                else
                {
                    // Give the selected history's async tables/challenges/phases
                    // a chance to publish while the independent live stream advances.
                    using var settled = CancellationTokenSource.CreateLinkedTokenSource(token);
                    settled.CancelAfter(TimeSpan.FromSeconds(10));
                    try { await WaitForMeasuredWorkAndIdle(settled.Token); }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                    var evidence = await Dispatcher.UIThread.InvokeAsync(() => new { RequestedStart = attempt.Start,
                        DisplayedStart = vm.CurrentlyDisplayedCombat?.StartTime,
                        StillSameLiveCombat = CombatIdentifier.CurrentCombat?.StartTime == attempt.LiveStart,
                        Ui = CaptureLiveUi(vm, monitor) }).GetTask().WaitAsync(token);
                    state.Timeline.Enqueue(new { Kind = "PublishedHistoryInspection", WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds, Evidence = evidence });
                    nextCurrent = evidence.StillSameLiveCombat && evidence.DisplayedStart == attempt.Start;
                    reviewedLiveStart = attempt.LiveStart;
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { state.Error(error); }
    }

    private sealed record LiveSelectionAttempt(DateTime Start, DateTime LiveStart, bool IsCurrent, Task<bool> Published);

    private sealed class LiveTimerObserver(LiveObservations state) : IDisposable
    {
        private readonly object _gate = new();
        private readonly List<WeakReference<TimerInstanceViewModel>> _timers = new();
        private readonly HashSet<int> _audioObserved = new();
        private sealed record MediaHook(WeakReference<object> Player, EventInfo Event, Delegate Handler);
        private readonly List<MediaHook> _mediaHooks = new();
        private readonly ConcurrentQueue<string> _mediaErrors = new();
        private long _triggered, _expired, _audioFlags;
        private long _visibleExpired, _subtimerExpired;
        private long _mediaPlaying;
        private long _probeTriggered, _probeAudioFlags, _probePlaying, _probeMediaErrors;
        internal void Subscribe() { TimerController.TimerTriggered += Triggered; TimerController.TimerExpired += Expired; }
        private void Triggered(TimerInstanceViewModel timer, Action<TimerInstanceViewModel> callback)
        {
            Interlocked.Increment(ref _triggered);
            if (timer.SourceTimer.Id == AudioProbeId) Interlocked.Increment(ref _probeTriggered);
            lock (_gate)
            {
                if (!_timers.Any(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, timer)))
                {
                    _timers.Add(new WeakReference<TimerInstanceViewModel>(timer));
                    timer.PropertyChanged += Changed;
                    ObserveMedia(timer);
                }
            }
            Record("TimerTriggered", timer);
        }
        private void Expired(TimerInstanceViewModel timer, Action<TimerInstanceViewModel> callback)
        {
            Interlocked.Increment(ref _expired);
            if (timer.SourceTimer.IsSubTimer) Interlocked.Increment(ref _subtimerExpired);
            else Interlocked.Increment(ref _visibleExpired);
            Record("TimerExpired", timer);
        }
        private void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(TimerInstanceViewModel.TimerValue) || sender is not TimerInstanceViewModel timer || !timer.SourceTimer.UseAudio) return;
            if (!(bool)typeof(TimerInstanceViewModel).GetField("_hasAudioPlayed", PrivateInstance)!.GetValue(timer)!) return;
            lock (_gate)
            {
                var key = _timers.FindIndex(reference => reference.TryGetTarget(out var existing) && ReferenceEquals(existing, timer));
                if (!_audioObserved.Add(key)) return;
            }
            Interlocked.Increment(ref _audioFlags);
            if (timer.SourceTimer.Id == AudioProbeId) Interlocked.Increment(ref _probeAudioFlags);
            Record("TimerAudioRequestFlagObserved", timer);
        }
        private void Record(string kind, TimerInstanceViewModel timer)
        {
            state.Timeline.Enqueue(new { Kind = kind, WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds,
                VirtualTime = TimeUtility.CorrectedTime, timer.SourceTimer.Id, timer.TimerName, timer.TimerValue,
                timer.TimerId, timer.SourceTimer.UseAudio, timer.SourceTimer.IsAlert, timer.SourceTimer.IsSubTimer,
                AudioLoaded = (bool)typeof(TimerInstanceViewModel).GetField("_audioLoaded", PrivateInstance)!.GetValue(timer)!,
                AudioRequestFlag = (bool)typeof(TimerInstanceViewModel).GetField("_hasAudioPlayed", PrivateInstance)!.GetValue(timer)! });
        }
        private void ObserveMedia(TimerInstanceViewModel timer)
        {
            var player = typeof(TimerInstanceViewModel).GetField("_mediaPlayer", PrivateInstance)!.GetValue(timer);
            if (player == null) return;
            var id = timer.SourceTimer.Id;
            var name = timer.TimerName;
            foreach (var eventName in new[] { "Playing", "EncounteredError" })
            {
                var kind = eventName;
                var info = player.GetType().GetEvent(kind);
                if (info == null) { _mediaErrors.Enqueue("Missing native media event " + kind); continue; }
                EventHandler<EventArgs> callback = (_, _) =>
                {
                    if (kind == "Playing") Interlocked.Increment(ref _mediaPlaying);
                    else _mediaErrors.Enqueue("Native media error: " + id + " / " + name);
                    if (id == AudioProbeId)
                    {
                        if (kind == "Playing") Interlocked.Increment(ref _probePlaying);
                        else Interlocked.Increment(ref _probeMediaErrors);
                    }
                    state.Timeline.Enqueue(new { Kind = "NativeMedia" + kind, WallMilliseconds = state.Clock.Elapsed.TotalMilliseconds, TimerId = id, TimerName = name });
                };
                var handler = Delegate.CreateDelegate(info.EventHandlerType!, callback.Target, callback.Method);
                info.AddEventHandler(player, handler);
                _mediaHooks.Add(new MediaHook(new WeakReference<object>(player), info, handler));
            }
        }
        internal object Summary() => new { TriggerEvents = _triggered, ExpiryEvents = _expired,
            VisibleTimerExpiryEvents = _visibleExpired, SubtimerExpiryEvents = _subtimerExpired,
            DistinctTimerInstances = _timers.Count, InstancesWithAudioRequestFlag = _audioFlags,
            NativeMediaPlayingEvents = _mediaPlaying, NativeMediaErrors = _mediaErrors.ToArray(),
            AudioProbe = new { Enabled = Program.AudioProbe, Triggers = _probeTriggered, AudioRequestFlags = _probeAudioFlags,
                NativePlayingEvents = _probePlaying, NativeErrors = _probeMediaErrors },
            AudioNote = "Flags show application request state, not verified audible playback. Alert playback may precede the trigger observer." };
        internal ReplayTimerStop CaptureStopState()
        {
            static object Identity(TimerInstanceViewModel timer) => new
            {
                timer.TimerId, timer.SourceTimer.Id, timer.TimerName, timer.IsTriggered, timer.TimerValue,
                timer.SourceTimer.IsSubTimer, timer.SourceTimer.TrackOutsideOfCombat
            };
            var controller = TimerController.GetActiveTimers().Values.Select(Identity).ToArray();
            // The public lookup is keyed by definition, so it cannot prove that
            // every hidden or multi-target instance was removed. Inspect each
            // model's authoritative dictionary after replay cleanup and UI idle.
            var definitions = ((IEnumerable<TimerInstance>)typeof(TimerController)
                .GetField("_availableTimers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).ToArray();
            var modelTimers = definitions.SelectMany(definition =>
                ((Dictionary<Guid, TimerInstanceViewModel>)typeof(TimerInstance)
                    .GetField("_activeTimerInstancesForTimer", PrivateInstance)!.GetValue(definition)!).Values)
                .Select(Identity).ToArray();
            lock (_gate)
            {
                // IsTriggered is the production isActive flag; both Complete and
                // Dispose clear it. Weak references do not extend timer lifetimes.
                var active = new List<object>();
                foreach (var reference in _timers)
                    if (reference.TryGetTarget(out var timer) && timer.IsTriggered) active.Add(Identity(timer));
                return new ReplayTimerStop(controller.Length, modelTimers.Length, active.Count, controller, modelTimers, active.ToArray());
            }
        }
        internal void ValidateAudioProbe()
        {
            if (Program.AudioProbe && (_probeTriggered == 0 || _probeAudioFlags == 0 || _probePlaying == 0 || _probeMediaErrors != 0))
                throw new InvalidOperationException("The requested diagnostic audio timer did not complete its trigger/request/native-playback checks; inspect TimerObservations.AudioProbe.");
        }
        public void Dispose()
        {
            TimerController.TimerTriggered -= Triggered;
            TimerController.TimerExpired -= Expired;
            lock (_gate)
            {
                foreach (var reference in _timers)
                    if (reference.TryGetTarget(out var timer)) timer.PropertyChanged -= Changed;
                foreach (var hook in _mediaHooks)
                    if (hook.Player.TryGetTarget(out var player))
                        try { hook.Event.RemoveEventHandler(player, hook.Handler); } catch (ObjectDisposedException) { }
                _mediaHooks.Clear();
            }
        }
    }
}
