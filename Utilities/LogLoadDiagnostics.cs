using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace SWTORCombatParser.Utilities;

/// <summary>Opt-in timings for the real log-loading pipeline. Disabled unless ORBS_LOG_LOAD_PROFILE is set.</summary>
public static class LogLoadDiagnostics
{
    private static readonly string? ReportPath = Environment.GetEnvironmentVariable("ORBS_LOG_LOAD_PROFILE");
    private static Capture? _capture;
    public static bool Enabled => !string.IsNullOrWhiteSpace(ReportPath);
    public static string? ProfileDataDirectory => Enabled
        ? Environment.GetEnvironmentVariable("ORBS_PROFILE_DATA_DIRECTORY") : null;
    public static bool KeepCombatsInMemory => Enabled &&
        Environment.GetEnvironmentVariable("ORBS_PROFILE_KEEP_COMBATS") == "1";
    public static int PendingMeasurements => _capture is { } capture ? Volatile.Read(ref capture.Pending) : 0;

    public static void Start(string logPath)
    {
        if (Enabled) _capture = new Capture(logPath);
    }

    public static IDisposable? Measure(string stage)
    {
        var capture = _capture;
        return capture == null ? null : new Measurement(capture, stage);
    }

    public static void WriteReport(string milestone)
    {
        var capture = _capture;
        if (capture == null || !Enabled) return;
        var report = new
        {
            Log = capture.LogPath,
            Milestone = milestone,
            CombatStorage = KeepCombatsInMemory ? "Memory" : "Disk cache",
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(capture.Started).TotalMilliseconds,
            AllocatedBytes = GC.GetTotalAllocatedBytes(false) - capture.AllocatedBytes,
            WorkingSetBytes = Environment.WorkingSet,
            Note = "Stage times include nested work; parallel per-combat sums overlap. Do not add all stages together.",
            Stages = capture.Stages.OrderBy(pair => pair.Value.FirstStarted).Select(pair => new
            {
                Name = pair.Key,
                Calls = pair.Value.Calls,
                TotalMilliseconds = pair.Value.ElapsedTicks * 1000d / Stopwatch.Frequency,
                FirstStartedMilliseconds = (pair.Value.FirstStarted - capture.Started) * 1000d / Stopwatch.Frequency
            }).ToArray()
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ReportPath!))!);
            File.WriteAllText(ReportPath!, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Debug.WriteLine("Could not write log-loading profile: " + error.Message);
        }
    }

    private sealed class Capture(string logPath)
    {
        internal readonly string LogPath = logPath;
        internal readonly long Started = Stopwatch.GetTimestamp();
        internal readonly long AllocatedBytes = GC.GetTotalAllocatedBytes(false);
        internal readonly ConcurrentDictionary<string, Stage> Stages = new();
        internal int Pending;
    }

    private sealed class Stage(long started)
    {
        internal readonly long FirstStarted = started;
        internal long ElapsedTicks;
        internal int Calls;
    }

    private sealed class Measurement : IDisposable
    {
        private readonly Capture _capture;
        private readonly string _name;
        private readonly long _started = Stopwatch.GetTimestamp();
        public Measurement(Capture capture, string name)
        {
            _capture = capture;
            _name = name;
            Interlocked.Increment(ref capture.Pending);
        }
        public void Dispose()
        {
            var stage = _capture.Stages.GetOrAdd(_name, _ => new Stage(_started));
            Interlocked.Add(ref stage.ElapsedTicks, Stopwatch.GetTimestamp() - _started);
            Interlocked.Increment(ref stage.Calls);
            Interlocked.Decrement(ref _capture.Pending);
        }
    }
}
