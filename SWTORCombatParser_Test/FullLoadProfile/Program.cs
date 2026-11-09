using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.AbilityInfo;
using SWTORCombatParser.Model.Challenge;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Challenges;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.Views;

internal static partial class Program
{
    internal static string LogPath = "";
    internal static string OutputDirectory = "";
    internal static bool HadFailure;
    private static int _result = 1;

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length < 2 || args.Skip(2).Any(arg => !IsProfileArgument(arg)))
        {
            Console.Error.WriteLine("Usage: FullLoadProfile <log-path> <new-output-directory> [--audit] [--selection-stress] [--switch] [--no-cache] [--exit] [--live-replay --replay-speed=30 --replay-from=ISO --replay-through=ISO --partial-write --audio-probe]");
            return 2;
        }
        var outputCreated = false;
        try
        {
            ReadLiveOptions(args);
            LogPath = Path.GetFullPath(args[0]);
            if (!File.Exists(LogPath)) throw new FileNotFoundException("The input combat log does not exist.", LogPath);
            OutputDirectory = Path.GetFullPath(args[1]);
            if (Directory.Exists(OutputDirectory) || File.Exists(OutputDirectory))
                throw new IOException("Use a new output directory for each run so stale reports cannot look like a successful check.");
            if (args.Contains("--no-cache") && args.Contains("--selection-stress"))
                throw new ArgumentException("Selection stress requires disk caching; omit --no-cache.");
            Directory.CreateDirectory(OutputDirectory);
            outputCreated = true;
            Environment.SetEnvironmentVariable("ORBS_LOG_LOAD_PROFILE", Path.Combine(OutputDirectory, "stage-report.json"));
            Environment.SetEnvironmentVariable("ORBS_PROFILE_KEEP_COMBATS", args.Contains("--no-cache") ? "1" : null);
            Environment.SetEnvironmentVariable("ORBS_PROFILE_DATA_DIRECTORY", Path.Combine(OutputDirectory, "settings"));
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                var path = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
                return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => File.WriteAllText(Path.Combine(OutputDirectory, "unhandled-error.txt"), e.ExceptionObject.ToString());
            IsolatePersistence();
            WriteBuildInfo();
            AppBuilder.Configure<FullLoadApp>().UsePlatformDetect().UseReactiveUI().LogToTrace()
                .StartWithClassicDesktopLifetime(args);
            return _result;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            if (outputCreated)
                File.WriteAllText(Path.Combine(OutputDirectory, "startup-error.txt"), error.ToString());
            return 1;
        }
    }

    internal static void Complete()
    {
        _result = HadFailure ? 1 : 0;
        File.WriteAllText(Path.Combine(OutputDirectory, "check-result.json"), JsonSerializer.Serialize(new
        {
            Status = HadFailure ? "Failed" : "Passed", ExitCode = _result,
            CompletedUtc = DateTime.UtcNow
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (Environment.GetCommandLineArgs().Contains("--exit"))
            Environment.Exit(_result); // Production close behavior otherwise leaves a tray process.
    }

    private static void IsolatePersistence()
    {
        var data = Path.Combine(OutputDirectory, "settings");
        Directory.CreateDirectory(data);
        var appDataRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser"));
        var appDataPrefix = appDataRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var redirected = new List<object>();
        var assembly = typeof(Settings).Assembly;
        var types = new[] {
            "Utilities.Settings", "Utilities.Logging", "Utilities.OrbsWindowManager", "Utilities.MetricColorLoader", "Utilities.ShouldShowPopup",
            "Model.Phases.DefaultPhaseManager", "Model.Timers.DefaultOrbsTimersManager", "Model.DataGrid.DataGridDefaults",
            "Model.Notes.RaidNotesReader", "Model.Challenge.DefaultChallengeManager", "Model.CloudRaiding.LeaderboardSettings",
            "Model.Overlays.DefaultPersonalOverlaysManager", "Model.Overlays.DefaultBossFrameManager", "Model.Overlays.DefaultGlobalOverlays",
            "Model.Overlays.DefaultCharacterOverlays", "Model.Overlays.RaidFrameOverlayManager", "Model.Overlays.DefaultRoomOverlayManager",
            "DataStructures.RoomOverlay.RoomOverlayLoader"
        };
        foreach (var suffix in types)
        {
            var type = assembly.GetType("SWTORCombatParser." + suffix)!;
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType != typeof(string)) continue;
                if (field.GetValue(null) is not string original || !Path.IsPathRooted(original)) continue;
                var fullPath = Path.GetFullPath(original);
                if (!fullPath.Equals(appDataRoot, StringComparison.OrdinalIgnoreCase) &&
                    !fullPath.StartsWith(appDataPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                // Restrict redirects to the actual persistence root, never a path
                // that merely contains the app's name (such as binary resources).
                // Fail closed if a future readonly/renamed field would escape isolation.
                if (field.IsInitOnly || (!field.Name.Contains("path", StringComparison.OrdinalIgnoreCase) &&
                                        !field.Name.Contains("file", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Cannot isolate persistent field {type.FullName}.{field.Name}.");
                var relativePath = Path.GetRelativePath(appDataRoot, fullPath);
                var path = relativePath == "." ? data : Path.Combine(data, relativePath);
                field.SetValue(null, path);
                redirected.Add(new { Type = suffix, Field = field.Name, RelativePath = relativePath });
            }
        }
        File.WriteAllText(Path.Combine(OutputDirectory, "persistence-isolation.json"),
            JsonSerializer.Serialize(new { RedirectedFields = redirected }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(data, "general_settings.json"), JsonSerializer.Serialize(new {
            offline_mode = true, Auto_Parse = false, current_tab = "data_grid", combat_logs_path = Path.GetDirectoryName(LogPath),
            overlay_bar_scale = 1.0, custom_audio_paths = Array.Empty<string>(), timers_enabled = LiveReplay
        }));
        var cache = Path.Combine(OutputDirectory, "combat-cache");
        Directory.CreateDirectory(cache);
        assembly.GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager")!
            .GetField("_sessionCacheDirectory", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, cache);
    }
}

public sealed partial class FullLoadApp : Application
{
    public override void Initialize()
    {
        if (Program.LiveReplay) InitializeLiveAudio();
        AvaloniaXamlLoader.Load(this);
        Resources["EnumDisplayNameConverter"] = new SWTORCombatParser.Utilities.Converters.EnumDisplayNameConverter();
        var converters = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "converter-resources.json")))!;
        foreach (var converter in converters)
        {
            var type = typeof(Settings).Assembly.GetType("SWTORCombatParser.Utilities.Converters." + converter["Type"])!;
            Resources[converter["Key"]] = Activator.CreateInstance(type, true)!;
        }
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (BindingPlugins.DataValidators.Count > 0) BindingPlugins.DataValidators.RemoveAt(0);
            IconGetter.Init();
            AbilityLoader.SetAbsorbAbilities();
            var challengeDefaults = SeedBuiltinChallengeDefaults();
            var main = new MainWindow();
            var viewModel = new MainWindowViewModel(main.HotkeyHandler);
            ValidateBuiltinPhaseDefaults(challengeDefaults);
            main.DataContext = viewModel;
            main.Title = "Orbs · Full load profiling";
            main.Width = 1400;
            main.Height = 900;
            desktop.MainWindow = main;
            var race = (RunRaceService)typeof(MainWindowViewModel).GetField("_runRaceService", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(viewModel)!;
            typeof(RunRaceService).GetField("_store", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(race, new RunGhostStore(Path.Combine(Program.OutputDirectory, "profile-ghosts.json")));
            main.Opened += async (_, _) =>
            {
                if (Program.LiveReplay)
                {
                    await RunLiveReplay(main, viewModel);
                    ScheduleLiveFinalMemory(viewModel);
                    return;
                }
                var loaded = await Load(main, viewModel);
                if (loaded && Environment.GetCommandLineArgs().Contains("--selection-stress"))
                    ScheduleStressFinalMemory(viewModel);
                else
                    Program.Complete();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static object SeedBuiltinChallengeDefaults()
    {
        // Normal startup loads these in the timer window's background task. Seed
        // the same shipped definitions first so the challenge viewmodel sees an
        // established settings store, independent of that first-run task's timing.
        DefaultChallengeManager.Init();
        typeof(Settings).Assembly.GetType("SWTORCombatParser.DataStructures.ChallengeInfo.ChallengeLoader", true)!
            .GetMethod("TryLoadChallenges", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null);
        var sources = DefaultChallengeManager.GetAllDefaults();
        var installed = sources.SelectMany(source => source.Challenges).ToArray();
        using var builtIn = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "DataStructures", "ChallengeInfo", "BuiltInChallenges.json")));
        var expectedIds = builtIn.RootElement.EnumerateArray()
            .SelectMany(source => source.GetProperty("Challenges").EnumerateArray())
            .Select(challenge => challenge.GetProperty("Id").GetGuid()).ToHashSet();
        var missingIds = expectedIds.Except(installed.Select(challenge => challenge.Id)).ToArray();
        if (expectedIds.Count == 0 || missingIds.Length > 0)
            throw new InvalidOperationException("The isolated UI check did not initialize all built-in challenges.");
        return new
        {
            SourceCount = sources.Count, BuiltInCount = expectedIds.Count, InstalledCount = installed.Length,
            EnabledCount = installed.Count(challenge => challenge.IsEnabled), MissingIds = missingIds,
            SeededBy = "DefaultChallengeManager.Init + ChallengeLoader.TryLoadChallenges"
        };
    }

    private static void ValidateBuiltinPhaseDefaults(object challengeDefaults)
    {
        // MainWindowViewModel installs these defaults before PhaseManager.Init.
        // A missing resource is logged and swallowed by production startup, so
        // explicitly reject that incomplete setup in the reproducible UI check.
        using var builtIn = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "DataStructures", "Phases", "phase_info.json")));
        using var installed = JsonDocument.Parse(File.ReadAllText(Path.Combine(Program.OutputDirectory,
            "settings", "phase_info.json")));
        var expectedIds = builtIn.RootElement.EnumerateArray().Select(phase => phase.GetProperty("Id").GetGuid()).ToHashSet();
        var installedIds = installed.RootElement.EnumerateArray().Select(phase => phase.GetProperty("Id").GetGuid()).ToHashSet();
        var missingIds = expectedIds.Except(installedIds).ToArray();
        var missingChallengePhaseIds = DefaultChallengeManager.GetAllDefaults().SelectMany(source => source.Challenges)
            .Where(challenge => challenge.ChallengeType == ChallengeType.MetricDuringPhase && !installedIds.Contains(challenge.PhaseId))
            .Select(challenge => new { challenge.Name, challenge.PhaseId }).ToArray();
        File.WriteAllText(Path.Combine(Program.OutputDirectory, "setup-report.json"), JsonSerializer.Serialize(new
        {
            Challenges = challengeDefaults,
            Phases = new { BuiltInCount = expectedIds.Count, InstalledCount = installedIds.Count, MissingIds = missingIds },
            MissingChallengePhaseIds = missingChallengePhaseIds
        }, new JsonSerializerOptions { WriteIndented = true }));
        if (expectedIds.Count == 0 || missingIds.Length > 0 || missingChallengePhaseIds.Length > 0)
            throw new InvalidOperationException("The isolated UI check did not initialize all built-in phases and challenge phase references.");
    }

    private static async Task<bool> Load(MainWindow window, MainWindowViewModel vm)
    {
        CombatMonitorViewModel? monitor = null;
        var expectedCombats = new Dictionary<DateTime, string>();
        Action<List<Combat>>? auditHandler = null;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            monitor = (CombatMonitorViewModel)typeof(MainWindowViewModel).GetField("_combatMonitorViewModel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
            var streamer = (CombatLogStreamer)typeof(CombatMonitorViewModel).GetField("_combatLogStreamer", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(monitor)!;
            if (Environment.GetCommandLineArgs().Contains("--audit"))
            {
                auditHandler = combats =>
                {
                    foreach (var original in combats) expectedCombats[original.StartTime] = CombatFingerprint(original);
                };
                monitor.OnHistoricalCombatsParsed += auditHandler;
            }
            var selected = new TaskCompletionSource<Combat>(TaskCreationOptions.RunContinuationsAsynchronously);
            Action<Combat> selectedHandler = combat => selected.TrySetResult(combat);
            CombatSelectionMonitor.CombatSelected += selectedHandler;
            monitor.Reset();
            LoadingWindowFactory.ShowLoading();
            streamer.ParseCompleteLog(Program.LogPath);
            var combat = await selected.Task.WaitAsync(TimeSpan.FromMinutes(10));
            CombatSelectionMonitor.CombatSelected -= selectedHandler;
            var pipeline = (Task)typeof(CombatLogStreamer).GetField("_monitoringTask", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(streamer)!;
            await pipeline.WaitAsync(TimeSpan.FromMinutes(10));
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
            {
                do
                {
                    await Task.Delay(50, timeout.Token);
                    await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                } while (LogLoadDiagnostics.PendingMeasurements > 0);
            }
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            LogLoadDiagnostics.WriteReport("Default combat selected; main-window UI reached application idle");
            File.WriteAllText(Path.Combine(Program.OutputDirectory, "ready.json"), JsonSerializer.Serialize(new {
                Log = Program.LogPath, Encounters = monitor.PastEncounters.Count,
                Combats = monitor.PastEncounters.Sum(e => e.EncounterCombats.Count),
                ResidentCombats = monitor.PastEncounters.Sum(e => e.EncounterCombats.Count(c => c.Combat != null)),
                SelectedCombat = combat.EncounterBossInfo, Start = combat.StartTime, End = combat.EndTime,
                PlayerMetrics = combat.CharacterParticipants.Values.OrderBy(p => p.Id).Select(p => new {
                    p.Id, p.Name, Damage = combat.TotalDamage.GetValueOrDefault(p),
                    Healing = combat.TotalEffectiveHealing.GetValueOrDefault(p),
                    Shielding = combat.TotalProvidedSheilding.GetValueOrDefault(p),
                    Threat = combat.TotalThreat.GetValueOrDefault(p)
                }).ToArray(),
                WindowTitle = window.Title
            }, new JsonSerializerOptions { WriteIndented = true }));
            // Exclude collection and inspection from the measured load interval.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            File.WriteAllText(Path.Combine(Program.OutputDirectory, "steady-memory.json"), JsonSerializer.Serialize(new {
                ManagedBytes = GC.GetTotalMemory(false),
                HeapBytes = GC.GetGCMemoryInfo().HeapSizeBytes,
                WorkingSetBytes = Environment.WorkingSet,
                PrivateBytes = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64,
                ResidentCombats = monitor.PastEncounters.Sum(e => e.EncounterCombats.Count(c => c.Combat != null)),
                CacheBytes = Directory.EnumerateFiles(Path.Combine(Program.OutputDirectory, "combat-cache")).Sum(path => new FileInfo(path).Length)
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (Environment.GetCommandLineArgs().Contains("--selection-stress"))
            {
                // The startup awaiter/result must not keep an old full combat alive
                // while this harness measures production selection ownership.
                combat = null!;
                selected = null!;
                selectedHandler = null!;
                await RunSelectionStress(vm, monitor);
            }
            if (Environment.GetCommandLineArgs().Contains("--switch"))
            {
                var orderedRows = monitor.PastEncounters.SelectMany(e => e.EncounterCombats)
                    .Where(c => !c.IsCurrentCombat && c.IsBossCombat).OrderBy(c => c.CombatStartTime).ToArray();
                var switches = new List<object>();
                foreach (var row in new[] { orderedRows[0], orderedRows[^1], orderedRows[0] })
                {
                    var switched = new TaskCompletionSource<Combat>(TaskCreationOptions.RunContinuationsAsynchronously);
                    Action<Combat> handler = value => switched.TrySetResult(value);
                    CombatSelectionMonitor.CombatSelected += handler;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    await Dispatcher.UIThread.InvokeAsync(row.SelectionToggle);
                    await switched.Task.WaitAsync(TimeSpan.FromSeconds(60));
                    CombatSelectionMonitor.CombatSelected -= handler;
                    using var switchTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    await WaitForMeasuredWorkAndIdle(switchTimeout.Token);
                    clock.Stop();
                    switches.Add(new { row.CombatLabel, row.CombatStartTime, Milliseconds = clock.Elapsed.TotalMilliseconds });
                }
                File.WriteAllText(Path.Combine(Program.OutputDirectory, "switch-times.json"), JsonSerializer.Serialize(switches,
                    new JsonSerializerOptions { WriteIndented = true }));
            }
            if (Environment.GetCommandLineArgs().Contains("--historical-loading"))
                await RunHistoricalLoadingChecks(window, vm, monitor);
            if (Environment.GetCommandLineArgs().Contains("--review-tabs"))
                await RunReviewTabChecks(window, vm, monitor);
            if (Environment.GetCommandLineArgs().Contains("--progression-review"))
                await RunProgressionReviewChecks(window, vm, monitor);
            if (Environment.GetCommandLineArgs().Contains("--audit"))
            {
                var mismatches = new List<string>();
                var audited = 0;
                await Task.Run(() =>
                {
                    foreach (var row in monitor.PastEncounters.SelectMany(e => e.EncounterCombats).Where(c => !c.IsCurrentCombat))
                    {
                        var loaded = row.GetCombat();
                        var actual = CombatFingerprint(loaded);
                        if (!expectedCombats.TryGetValue(row.CombatStartTime, out var expected) || actual != expected)
                        {
                            mismatches.Add(row.CombatStartTime.ToString("O"));
                            File.WriteAllText(Path.Combine(Program.OutputDirectory, $"mismatch-{audited}.json"),
                                JsonSerializer.Serialize(new { row.CombatStartTime, row.CombatLoadError, Expected = expected, Actual = actual }));
                        }
                        if (!row.IsSelected) row.UnloadCombat();
                        audited++;
                    }
                });
                File.WriteAllText(Path.Combine(Program.OutputDirectory, "parity-audit.json"), JsonSerializer.Serialize(new {
                    Audited = audited, Mismatches = mismatches
                }, new JsonSerializerOptions { WriteIndented = true }));
                using var fingerprintFile = File.Create(Path.Combine(Program.OutputDirectory, "fingerprints.json.br"));
                using var fingerprintStream = new System.IO.Compression.BrotliStream(fingerprintFile,
                    System.IO.Compression.CompressionLevel.Fastest);
                JsonSerializer.Serialize(fingerprintStream, expectedCombats);
                expectedCombats.Clear();
                if (mismatches.Count > 0 || audited == 0)
                    Program.HadFailure = true;
            }
            return true;
        }
        catch (Exception error)
        {
            LogLoadDiagnostics.WriteReport("Load failed");
            File.WriteAllText(Path.Combine(Program.OutputDirectory, "load-error.txt"), error.ToString());
            Program.HadFailure = true;
            return false;
        }
        finally
        {
            if (monitor != null && auditHandler != null)
                monitor.OnHistoricalCombatsParsed -= auditHandler;
            expectedCombats.Clear();
        }
    }

    private sealed record StressResource(
        double ElapsedMilliseconds, long ManagedBytes, long AllocatedBytes, long WorkingSetBytes,
        long PrivateBytes, double ProcessCpuMilliseconds, int Gen0Collections, int Gen1Collections,
        int Gen2Collections, int ResidentCombats, int PendingMeasurements, long HeapBytes,
        long FragmentedBytes, long TotalCommittedBytes, long TotalAvailableMemoryBytes);

    private sealed record StressClick(int Index, DateTime CombatStart, string Label, long CacheBytes,
        double ClickMilliseconds, bool WasResidentBeforeClick);

    private sealed record StressPublish(int ClickCount, DateTime CombatStart, string Boss,
        double ElapsedMilliseconds, bool MatchesLatestRequest);

    private sealed record PlotSnapshot(DateTime? SelectedStart, string SelectedNumericFingerprint,
        DateTime[] RetainedCombatStarts, long ScatterPointCount, long AnnotationCount,
        int TooltipCount, object[] Series);

    private sealed record ChallengeParticipantSnapshot(string? KeyName, bool SecondaryMetric,
        long? ParticipantId, long? ParticipantLogId, string? ParticipantName, double Value);

    private sealed record ChallengeResultSnapshot(Guid Id, string Name, string Type, string? DisplayedTotal,
        double NumericTotal, DateTime? RequestedCombatStart, bool RequestedCombatMatchesSelection,
        ChallengeParticipantSnapshot[] Participants);

    private sealed record ChallengeSnapshot(DateTime SelectedStart, int ActiveCount, string NumericFingerprint,
        bool AllRequestedCombatsMatchSelection, ChallengeResultSnapshot[] Challenges);

    private sealed record ObservedCombat(int InstanceId, DateTime Start, string Boss, WeakReference<Combat> Reference);
    private sealed record SurvivingCombat(int InstanceId, DateTime Start, string Boss, bool IsDisplayedInstance);
    private sealed record CombatRetentionSnapshot(int ObservedInstances, int SurvivingInstances,
        int OlderSurvivingInstances, SurvivingCombat[] Survivors);
    private static readonly object ObservedCombatsLock = new();
    private static readonly List<ObservedCombat> ObservedCombats = new();

    private sealed class StressRound
    {
        public string Name { get; init; } = "";
        public string Status { get; set; } = "Running";
        public string? Error { get; set; }
        public StressResource? Baseline { get; set; }
        public StressResource? AfterReady { get; set; }
        public StressResource? AfterGcFifthSelected { get; set; }
        public StressResource? AfterGcOriginalRestored { get; set; }
        public PlotSnapshot? BaselinePlot { get; set; }
        public PlotSnapshot? FifthSelectedPlot { get; set; }
        public PlotSnapshot? OriginalRestoredPlot { get; set; }
        public bool? RestoredPlotMatchesBaseline { get; set; }
        public ChallengeSnapshot? BaselineChallenges { get; set; }
        public ChallengeSnapshot? FifthSelectedChallenges { get; set; }
        public ChallengeSnapshot? OriginalRestoredChallenges { get; set; }
        public bool? RestoredChallengesMatchBaseline { get; set; }
        public List<StressClick> Clicks { get; } = new();
        public List<object> SequentialSelections { get; } = new();
        public StressPublish[] Publications { get; set; } = Array.Empty<StressPublish>();
        public double WorkloadMilliseconds { get; set; }
        public double LastClickToReadyMilliseconds { get; set; }
        public double ObservationMilliseconds { get; set; }
        public object? Peaks { get; set; }
        public object? Cpu { get; set; }
        public object? FinalSelection { get; set; }
        public int SampleCount { get; set; }
    }

    private static async Task RunSelectionStress(MainWindowViewModel vm, CombatMonitorViewModel monitor)
    {
        var rounds = new List<StressRound>();
        var samplesByRound = new Dictionary<string, StressResource[]>();
        object[] chosenIdentities = Array.Empty<object>();
        object? originalIdentity = null;
        string? failure = null;
        lock (ObservedCombatsLock) ObservedCombats.Clear();
        CombatSelectionMonitor.CombatSelected += ObserveCombat;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => ObserveCombat(vm.CurrentlyDisplayedCombat));
            // Capture the collections once on the UI thread. The sampler only reads
            // atomic Combat references from these rows; it never enumerates UI lists.
            var allRows = await Dispatcher.UIThread.InvokeAsync(() =>
                monitor.PastEncounters.SelectMany(e => e.EncounterCombats).ToArray());
            var original = await Dispatcher.UIThread.InvokeAsync(() => allRows.Single(row => row.IsSelected));
            originalIdentity = RowIdentity(original);
            var chosen = allRows.Where(row => !ReferenceEquals(row, original) && !row.IsCurrentCombat &&
                    row.IsBossCombat && !string.IsNullOrEmpty(row.CombatCachePath) && File.Exists(row.CombatCachePath))
                .OrderByDescending(CacheBytes).ThenBy(row => row.CombatStartTime).Take(5).ToArray();
            if (chosen.Length != 5)
                throw new InvalidOperationException("Selection stress requires five cache-backed completed boss combats besides the initial selection.");
            chosenIdentities = chosen.Select(RowIdentity).ToArray();

            foreach (var rapid in new[] { false, true })
            {
                var round = new StressRound { Name = rapid ? "RapidFive" : "SequentialFive" };
                rounds.Add(round);
                await SelectAndSettle(original);
                round.BaselinePlot = await Dispatcher.UIThread.InvokeAsync(() => CapturePlot(vm));
                round.BaselineChallenges = await Dispatcher.UIThread.InvokeAsync(() => CaptureChallenges(vm));
                ValidateChallenges(round.BaselineChallenges);
                await CollectOutsideLatency();
                round.Baseline = CaptureResource(allRows, 0);
                await RunStressRound(vm, allRows, chosen, rapid, round, samplesByRound);
                if (round.Status != "Passed")
                    throw new InvalidOperationException(round.Error ?? (round.Name + " failed verification."));

                // Returning to the same combat makes retained-memory comparisons
                // meaningful, and leaves all five test rows unloaded for the next run.
                await SelectAndSettle(original);
                round.OriginalRestoredPlot = await Dispatcher.UIThread.InvokeAsync(() => CapturePlot(vm));
                round.OriginalRestoredChallenges = await Dispatcher.UIThread.InvokeAsync(() => CaptureChallenges(vm));
                ValidateChallenges(round.OriginalRestoredChallenges);
                await CollectOutsideLatency();
                round.AfterGcOriginalRestored = CaptureResource(allRows, 0);
                round.RestoredPlotMatchesBaseline = round.OriginalRestoredPlot.SelectedStart == round.BaselinePlot.SelectedStart &&
                    round.OriginalRestoredPlot.SelectedNumericFingerprint == round.BaselinePlot.SelectedNumericFingerprint;
                if (round.RestoredPlotMatchesBaseline != true)
                    throw new InvalidOperationException("The restored combat's plot differs from its baseline.");
                if (round.OriginalRestoredPlot.RetainedCombatStarts.Any(start => start != original.CombatStartTime))
                    throw new InvalidOperationException("The plot retained data from a combat that is no longer displayed.");
                round.RestoredChallengesMatchBaseline = round.OriginalRestoredChallenges.SelectedStart == round.BaselineChallenges.SelectedStart &&
                    round.OriginalRestoredChallenges.NumericFingerprint == round.BaselineChallenges.NumericFingerprint;
                if (round.RestoredChallengesMatchBaseline != true)
                    throw new InvalidOperationException("The restored combat's challenge results differ from their baseline.");
            }
        }
        catch (Exception error)
        {
            failure = error.ToString();
        }
        finally
        {
            CombatSelectionMonitor.CombatSelected -= ObserveCombat;
            if (failure != null || rounds.Count != 2 || rounds.Any(round => round.Status != "Passed"))
                Program.HadFailure = true;
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(Path.Combine(Program.OutputDirectory, "selection-stress-samples.json"),
                JsonSerializer.Serialize(samplesByRound, options));
            File.WriteAllText(Path.Combine(Program.OutputDirectory, "selection-stress.json"), JsonSerializer.Serialize(new
            {
                Status = failure == null && rounds.Count == 2 && rounds.All(r => r.Status == "Passed") ? "Passed" : "Failed",
                Error = failure, OriginalSelection = originalIdentity, ChosenCombats = chosenIdentities,
                SampleTargetMilliseconds = 50, RapidClickTargetGapMilliseconds = 75,
                QuietObservationMilliseconds = 500,
                Notes = "Latency excludes forced GC and restoring the original selection. Readiness requires the requested combat publication, two idle passes with no diagnostic work, and correct displayed selection. Per-round post-GC samples are immediate/transient: async continuations or deferred UI cleanup may still retain objects. Use selection-stress-final-memory.json, collected after all stress scopes return, to assess settled retained growth against Baseline. Memory after fifth selection can also differ because the combat differs. CPU percentages include this process across all cores; peak resources are sampled, not exact maxima. Caches are already on disk; every tested row must be unloaded at its click.",
                Rounds = rounds
            }, options));
        }
    }

    private static void ScheduleStressFinalMemory(MainWindowViewModel vm)
    {
        var monitor = (CombatMonitorViewModel)typeof(MainWindowViewModel)
            .GetField("_combatMonitorViewModel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(vm)!;
        // Post from the Opened handler only after Load has returned. The callback
        // captures app objects alone, never Load's completed task/await temporaries.
        Dispatcher.UIThread.Post(async () =>
        {
            try { await CaptureStressFinalMemory(vm, monitor); }
            catch (Exception error)
            {
                Program.HadFailure = true;
                File.WriteAllText(Path.Combine(Program.OutputDirectory, "final-memory-error.txt"), error.ToString());
            }
            Program.Complete();
        });
    }

    private static async Task CaptureStressFinalMemory(MainWindowViewModel vm, CombatMonitorViewModel monitor)
    {
        var measurements = new List<object>();
        string? failure = null;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var rows = await Dispatcher.UIThread.InvokeAsync(() => monitor.PastEncounters
                .SelectMany(encounter => encounter.EncounterCombats).ToArray());
            for (var index = 0; index < 3; index++)
            {
                var untilSample = TimeSpan.FromSeconds(5 * (index + 1)) - clock.Elapsed;
                if (untilSample > TimeSpan.Zero)
                    await Task.Delay(untilSample, timeout.Token);
                await WaitForMeasuredWorkAndIdle(timeout.Token);
                var plot = await Dispatcher.UIThread.InvokeAsync(() => CapturePlot(vm));
                var challenges = await Dispatcher.UIThread.InvokeAsync(() => CaptureChallenges(vm));
                ValidateChallenges(challenges);
                await CollectOutsideLatency();
                var resources = CaptureResource(rows, clock.Elapsed.TotalMilliseconds);
                var retainedCombats = await Dispatcher.UIThread.InvokeAsync(() => CaptureSurvivingCombats(vm));
                measurements.Add(new { Index = index + 1, Resources = resources, Plot = plot, Challenges = challenges, RetainedCombats = retainedCombats });
            }
        }
        catch (Exception error) { failure = error.ToString(); }
        if (failure != null) Program.HadFailure = true;
        File.WriteAllText(Path.Combine(Program.OutputDirectory, "selection-stress-final-memory.json"),
            JsonSerializer.Serialize(new
            {
                Status = failure == null ? "Completed" : "Failed", Error = failure,
                Note = "UI-idle/forced-GC snapshots at approximately 5, 10 and 15 seconds after both RunSelectionStress and the entire startup Load method return. The posted callback captures only the live app viewmodel and monitor. The original combat was restored by the stress test. These samples are outside all selection latencies. Heap/fragmentation/committed values describe the latest GC; managed bytes are sampled after that collection. RetainedCombats inspects only weak references to distinct instances published during stress; older survivors are observations, not automatic failures, because intended owners need separate inspection.",
                Samples = measurements
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task RunStressRound(MainWindowViewModel vm, PastCombat[] allRows, PastCombat[] chosen,
        bool rapid, StressRound result, Dictionary<string, StressResource[]> samplesByRound)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        using var samplingStop = new CancellationTokenSource();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var samples = new System.Collections.Concurrent.ConcurrentQueue<StressResource>();
        var publications = new System.Collections.Concurrent.ConcurrentQueue<StressPublish>();
        var requested = -1;
        var awaited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Combat> handler = combat =>
        {
            var index = Volatile.Read(ref requested);
            var matches = index >= 0 && combat.StartTime == chosen[index].CombatStartTime;
            publications.Enqueue(new StressPublish(index + 1, combat.StartTime, combat.EncounterBossInfo,
                clock.Elapsed.TotalMilliseconds, matches));
            if (matches) awaited.TrySetResult(true);
        };
        CombatSelectionMonitor.CombatSelected += handler;
        samples.Enqueue(result.Baseline!);
        var sampling = Task.Run(async () =>
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
                while (await timer.WaitForNextTickAsync(samplingStop.Token))
                    samples.Enqueue(CaptureResource(allRows, clock.Elapsed.TotalMilliseconds));
            }
            catch (OperationCanceledException) when (samplingStop.IsCancellationRequested) { }
        });
        try
        {
            for (var index = 0; index < chosen.Length; index++)
            {
                var row = chosen[index];
                var clickIndex = index;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    awaited = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    Volatile.Write(ref requested, clickIndex);
                    result.Clicks.Add(new StressClick(clickIndex + 1, row.CombatStartTime, row.CombatLabel,
                        CacheBytes(row), clock.Elapsed.TotalMilliseconds, row.Combat != null));
                    row.SelectionToggle();
                }).GetTask().WaitAsync(timeout.Token);

                if (rapid && index != chosen.Length - 1)
                {
                    await Task.Delay(75, timeout.Token);
                    continue;
                }

                await awaited.Task.WaitAsync(timeout.Token);
                await WaitForMeasuredWorkAndIdle(timeout.Token);
                if (!rapid)
                {
                    var displayed = await Dispatcher.UIThread.InvokeAsync(() => vm.CurrentlyDisplayedCombat?.StartTime)
                        .GetTask().WaitAsync(timeout.Token);
                    var selectionMilliseconds = clock.Elapsed.TotalMilliseconds - result.Clicks[^1].ClickMilliseconds;
                    var challenges = await Dispatcher.UIThread.InvokeAsync(() => CaptureChallenges(vm))
                        .GetTask().WaitAsync(timeout.Token);
                    result.SequentialSelections.Add(new
                    {
                        Index = index + 1, row.CombatStartTime,
                        Milliseconds = selectionMilliseconds,
                        DisplayedCorrectCombat = displayed == row.CombatStartTime,
                        Challenges = challenges
                    });
                    if (displayed != row.CombatStartTime)
                        throw new InvalidOperationException("Sequential selection settled on a different combat.");
                    ValidateChallenges(challenges);
                    LogLoadDiagnostics.WriteReport("Selection stress: " + result.Name + " combat " + (index + 1));
                    File.Copy(Path.Combine(Program.OutputDirectory, "stage-report.json"),
                        Path.Combine(Program.OutputDirectory, $"stages-{result.Name}-{index + 1}.json"));
                }
            }
            result.WorkloadMilliseconds = clock.Elapsed.TotalMilliseconds;
            result.LastClickToReadyMilliseconds = result.WorkloadMilliseconds - result.Clicks[^1].ClickMilliseconds;
            result.AfterReady = CaptureResource(allRows, result.WorkloadMilliseconds);
            samples.Enqueue(result.AfterReady);

            // Keep observing after readiness so a late older worker cannot silently
            // publish after the final requested combat appears.
            var observedAt = clock.Elapsed.TotalMilliseconds;
            await Task.Delay(500, timeout.Token);
            await WaitForMeasuredWorkAndIdle(timeout.Token);
            result.ObservationMilliseconds = clock.Elapsed.TotalMilliseconds - observedAt;
            var final = await Dispatcher.UIThread.InvokeAsync(() => new
            {
                ExpectedStart = chosen[^1].CombatStartTime,
                DisplayedStart = vm.CurrentlyDisplayedCombat?.StartTime,
                DisplayedBoss = vm.CurrentlyDisplayedCombat?.EncounterBossInfo,
                SelectedStarts = allRows.Where(row => row.IsSelected).Select(row => row.CombatStartTime).ToArray()
            }).GetTask().WaitAsync(timeout.Token);
            result.FinalSelection = final;
            if (final.DisplayedStart != final.ExpectedStart || final.SelectedStarts.Length != 1 || final.SelectedStarts[0] != final.ExpectedStart)
                throw new InvalidOperationException("The final fifth combat is not the sole selected and displayed combat.");
            if (publications.Any(p => p.ClickCount == chosen.Length && !p.MatchesLatestRequest))
                throw new InvalidOperationException("An older combat was published after the fifth click.");
            if (result.Clicks.Any(c => c.WasResidentBeforeClick))
                throw new InvalidOperationException("At least one test combat was already resident at its click.");
            result.FifthSelectedChallenges = await Dispatcher.UIThread.InvokeAsync(() => CaptureChallenges(vm))
                .GetTask().WaitAsync(timeout.Token);
            ValidateChallenges(result.FifthSelectedChallenges);
            result.Status = "Passed";
        }
        catch (Exception error)
        {
            result.Status = "Failed";
            result.Error = error.ToString();
        }
        finally
        {
            CombatSelectionMonitor.CombatSelected -= handler;
            samplingStop.Cancel();
            try { await sampling; }
            catch (Exception error)
            {
                result.Status = "Failed";
                result.Error = (result.Error ?? "") + "\nResource sampling failed: " + error;
            }
            result.Publications = publications.ToArray();
            var captured = samples.ToArray();
            samplesByRound[result.Name] = captured;
            result.SampleCount = captured.Length;
            result.Peaks = new
            {
                ManagedBytes = captured.Max(s => s.ManagedBytes),
                AllocatedBytes = captured.Max(s => s.AllocatedBytes) - result.Baseline!.AllocatedBytes,
                WorkingSetBytes = captured.Max(s => s.WorkingSetBytes),
                PrivateBytes = captured.Max(s => s.PrivateBytes),
                ResidentCombats = captured.Max(s => s.ResidentCombats),
                PendingMeasurements = captured.Max(s => s.PendingMeasurements)
            };
            if (result.AfterReady != null)
            {
                var cpuMilliseconds = result.AfterReady.ProcessCpuMilliseconds - result.Baseline!.ProcessCpuMilliseconds;
                result.Cpu = new
                {
                    ProcessCpuMilliseconds = cpuMilliseconds, LogicalProcessors = Environment.ProcessorCount,
                    AverageCoreEquivalents = cpuMilliseconds / result.WorkloadMilliseconds,
                    AverageMachinePercent = cpuMilliseconds / result.WorkloadMilliseconds / Environment.ProcessorCount * 100
                };
            }
        }
        if (result.Status == "Passed")
        {
            result.FifthSelectedPlot = await Dispatcher.UIThread.InvokeAsync(() => CapturePlot(vm));
            await CollectOutsideLatency();
            result.AfterGcFifthSelected = CaptureResource(allRows, 0);
        }
    }

    private static async Task SelectAndSettle(PastCombat row)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var selected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<Combat> handler = combat => { if (combat.StartTime == row.CombatStartTime) selected.TrySetResult(true); };
        CombatSelectionMonitor.CombatSelected += handler;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(row.SelectionToggle).GetTask().WaitAsync(timeout.Token);
            await selected.Task.WaitAsync(timeout.Token);
            await WaitForMeasuredWorkAndIdle(timeout.Token);
        }
        finally { CombatSelectionMonitor.CombatSelected -= handler; }
    }

    private static async Task WaitForMeasuredWorkAndIdle(CancellationToken token)
    {
        var quietPasses = 0;
        while (quietPasses < 2)
        {
            await Task.Delay(50, token);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).GetTask().WaitAsync(token);
            quietPasses = LogLoadDiagnostics.PendingMeasurements == 0 ? quietPasses + 1 : 0;
        }
    }

    private static async Task CollectOutsideLatency()
    {
        await Task.Run(() => { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); });
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static StressResource CaptureResource(PastCombat[] rows, double elapsedMilliseconds)
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        var heap = GC.GetGCMemoryInfo();
        return new StressResource(elapsedMilliseconds, GC.GetTotalMemory(false), GC.GetTotalAllocatedBytes(false),
            process.WorkingSet64, process.PrivateMemorySize64, process.TotalProcessorTime.TotalMilliseconds,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            rows.Count(row => row.Combat != null), LogLoadDiagnostics.PendingMeasurements,
            heap.HeapSizeBytes, heap.FragmentedBytes, heap.TotalCommittedBytes, heap.TotalAvailableMemoryBytes);
    }

    private static long CacheBytes(PastCombat row) => new FileInfo(row.CombatCachePath).Length;
    private static object RowIdentity(PastCombat row) => new
    {
        row.CombatStartTime, row.CombatLabel, row.CombatDurationSeconds,
        CacheBytes = !string.IsNullOrEmpty(row.CombatCachePath) && File.Exists(row.CombatCachePath) ? CacheBytes(row) : 0
    };

    private static PlotSnapshot CapturePlot(MainWindowViewModel vm)
    {
        // Reflection keeps this standalone harness independent of ScottPlot's compile
        // references. Only scalar counts and a hash survive this inspection, which
        // runs on the UI thread before the out-of-band collection/memory sample.
        var plot = typeof(MainWindowViewModel).GetField("_plotViewModel", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
        var series = ((System.Collections.IEnumerable)plot.GetType().GetField("_seriesToPlot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(plot)!).Cast<object>().OrderBy(value => (string)value.GetType().GetField("Name")!.GetValue(value)!, StringComparer.Ordinal);
        var selectedStart = vm.CurrentlyDisplayedCombat?.StartTime;
        var allDates = new HashSet<DateTime>();
        var details = new List<object>();
        long totalPoints = 0, totalAnnotations = 0;
        var totalTooltips = 0;
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var pair = new byte[16];
        foreach (var value in series)
        {
            var type = value.GetType();
            var name = (string)type.GetField("Name")!.GetValue(value)!;
            var dates = new HashSet<DateTime>();
            var entryCounts = new Dictionary<string, int>();
            long points = 0, annotations = 0;
            var tooltips = 0;
            foreach (var family in new[] { "Points", "Line", "EffectivePoints", "EffectiveLine", "PointsByCharacter", "LineByCharacter", "Abilities", "Tooltip", "EffectiveTooltip" })
            {
                var dictionary = (System.Collections.IDictionary)type.GetProperty(family)!.GetValue(value)!;
                entryCounts[family] = dictionary.Count;
                foreach (System.Collections.DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is DateTime date)
                    {
                        dates.Add(date);
                        allDates.Add(date);
                    }
                    if (family == "Abilities")
                    {
                        annotations += ((System.Collections.ICollection)entry.Value!).Count;
                        continue;
                    }
                    if (family is "Tooltip" or "EffectiveTooltip")
                    {
                        tooltips++;
                        continue;
                    }
                    var scatter = entry.Value!;
                    var data = scatter.GetType().GetProperty("Data")!.GetValue(scatter)!;
                    var sourceContract = data.GetType().GetInterfaces().Single(contract => contract.FullName == "ScottPlot.IScatterSource");
                    var coordinates = (System.Collections.IEnumerable)sourceContract.GetMethod("GetScatterPoints")!.Invoke(data, null)!;
                    var selected = entry.Key is DateTime start && start == selectedStart;
                    if (selected)
                        hash.AppendData(System.Text.Encoding.UTF8.GetBytes(name + "|" + family + "\n"));
                    foreach (var coordinate in coordinates)
                    {
                        points++;
                        if (!selected) continue;
                        var coordinateType = coordinate.GetType();
                        // ScottPlot 5 Coordinates exposes X/Y as properties, while
                        // CombatMetaDataSeries.Name is a field.
                        var x = (double)(coordinateType.GetProperty("X")?.GetValue(coordinate)
                            ?? throw new MissingMemberException(coordinateType.FullName, "X"));
                        var y = (double)(coordinateType.GetProperty("Y")?.GetValue(coordinate)
                            ?? throw new MissingMemberException(coordinateType.FullName, "Y"));
                        BitConverter.TryWriteBytes(pair.AsSpan(0, 8), x);
                        BitConverter.TryWriteBytes(pair.AsSpan(8, 8), y);
                        hash.AppendData(pair);
                    }
                }
            }
            totalPoints += points;
            totalAnnotations += annotations;
            totalTooltips += tooltips;
            details.Add(new { Name = name, RetainedCombatStarts = dates.OrderBy(date => date).ToArray(),
                DictionaryEntries = entryCounts, ScatterPointCount = points, AnnotationCount = annotations, TooltipCount = tooltips });
        }
        return new PlotSnapshot(selectedStart, Convert.ToHexString(hash.GetHashAndReset()),
            allDates.OrderBy(date => date).ToArray(), totalPoints, totalAnnotations, totalTooltips, details.ToArray());
    }

    private static ChallengeSnapshot CaptureChallenges(MainWindowViewModel vm)
    {
        // Called only after readiness, on the UI thread. Convert every reference
        // immediately to scalar identity/value data so this probe cannot retain combats.
        var selected = vm.CurrentlyDisplayedCombat ?? throw new InvalidOperationException("No displayed combat for challenge verification.");
        var window = ChallengeSetupViewModel._challengeWindowViewModel
            ?? throw new InvalidOperationException("The challenge window viewmodel is unavailable.");
        var requestedField = typeof(ChallengeInstanceViewModel).GetField("_requestedCombat", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(ChallengeInstanceViewModel).FullName, "_requestedCombat");
        var challenges = window.ActiveChallengeInstances.OrderBy(challenge => challenge.SourceChallengeId).Select(challenge =>
        {
            var requested = (Combat?)requestedField.GetValue(challenge);
            var participants = challenge._metricBarsDict.OrderBy(pair => pair.Key.Item1, StringComparer.Ordinal)
                .ThenBy(pair => pair.Key.Item2).Select(pair => new ChallengeParticipantSnapshot(
                    pair.Key.Item1, pair.Key.Item2, pair.Value.Player?.Id, pair.Value.Player?.LogId,
                    pair.Value.Player?.Name, pair.Value.Value)).ToArray();
            return new ChallengeResultSnapshot(challenge.SourceChallengeId, challenge.ChallengeName,
                challenge.Type.ToString(), challenge.MetricTotal,
                participants.Where(participant => !participant.SecondaryMetric).Sum(participant => participant.Value),
                requested?.StartTime, ReferenceEquals(requested, selected), participants);
        }).ToArray();
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(challenges)));
        return new ChallengeSnapshot(selected.StartTime, challenges.Length, fingerprint,
            challenges.All(challenge => challenge.RequestedCombatMatchesSelection), challenges);
    }

    private static void ValidateChallenges(ChallengeSnapshot snapshot)
    {
        if (!snapshot.AllRequestedCombatsMatchSelection)
            throw new InvalidOperationException("Challenge metrics retain a different combat than the displayed selection: " +
                string.Join(", ", snapshot.Challenges.Where(challenge => !challenge.RequestedCombatMatchesSelection)
                    .Select(challenge => $"{challenge.Id} ({challenge.Name}), requested {challenge.RequestedCombatStart:O}, displayed {snapshot.SelectedStart:O}")));
    }

    private static void ObserveCombat(Combat? combat)
    {
        if (combat == null) return;
        lock (ObservedCombatsLock)
        {
            if (ObservedCombats.Any(observed => observed.Reference.TryGetTarget(out var existing) && ReferenceEquals(existing, combat)))
                return;
            ObservedCombats.Add(new ObservedCombat(ObservedCombats.Count + 1, combat.StartTime,
                combat.EncounterBossInfo, new WeakReference<Combat>(combat)));
        }
    }

    private static CombatRetentionSnapshot CaptureSurvivingCombats(MainWindowViewModel vm)
    {
        lock (ObservedCombatsLock)
        {
            var survivors = new List<SurvivingCombat>();
            foreach (var observed in ObservedCombats)
                if (observed.Reference.TryGetTarget(out var combat))
                    survivors.Add(new SurvivingCombat(observed.InstanceId, observed.Start, observed.Boss,
                        ReferenceEquals(combat, vm.CurrentlyDisplayedCombat)));
            return new CombatRetentionSnapshot(ObservedCombats.Count, survivors.Count,
                survivors.Count(combat => !combat.IsDisplayedInstance), survivors.ToArray());
        }
    }

    private static string CombatFingerprint(Combat combat)
    {
        var state = CombatLogStateBuilder.CurrentState;
        var snapshot = new {
            combat.StartTime, combat.EndTime, combat.EncounterBossInfo, combat.WasBossKilled,
            Entities = combat.AllEntities.Values.OrderBy(p => p.Id).Select(p => new {
                p.Id, p.LogId, p.Name,
                Damage = combat.TotalDamage.GetValueOrDefault(p), Healing = combat.TotalEffectiveHealing.GetValueOrDefault(p),
                Shielding = combat.TotalProvidedSheilding.GetValueOrDefault(p), Threat = combat.TotalThreat.GetValueOrDefault(p),
                Cooldown = combat.AverageDamageSavedDuringCooldown.GetValueOrDefault(p),
                Class = state.GetCharacterClassAtTime(p, combat.StartTime)?.Discipline,
                Effects = state.GetEffectsWithTarget(combat.StartTime, combat.EndTime, p)
                    .OrderBy(m => m.EffectId).ThenBy(m => m.Source.Id).ThenBy(m => m.StartTime).ThenBy(m => m.StopTime)
                    .Select(m => new { m.EffectId, Source = m.Source.Id, Target = m.Target.Id, m.StartTime, m.StopTime,
                        Charges = m.ChargesAtTime.OrderBy(c => c.Key).ToArray() }).ToArray()
            }).ToArray()
        };
        return JsonSerializer.Serialize(snapshot);
    }
}
