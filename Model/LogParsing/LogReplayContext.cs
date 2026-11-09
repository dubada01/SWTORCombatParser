using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace SWTORCombatParser.Model.LogParsing;

/// <summary>Owns the simulator clock while the normal live parser consumes a generated log.</summary>
public sealed class LogReplayContext : IDisposable
{
    private static LogReplayContext? _current;
    private readonly DateTime _anchor;
    private readonly double _speedMultiplier;
    private readonly Stopwatch _clock = new();
    private sealed record DeliveryWatermark(DateTime Through, long FlushedBytes);
    private DeliveryWatermark _delivery;
    private LogReplayContext(DateTime anchor, double speedMultiplier)
    {
        _anchor = anchor;
        _speedMultiplier = speedMultiplier;
        _delivery = new DeliveryWatermark(anchor, 0);
    }
    public static bool IsActive => Volatile.Read(ref _current) != null;
    public static DateTime? CurrentTime
    {
        get { var current = Volatile.Read(ref _current); return current == null ? null : current._anchor + current._clock.Elapsed * current._speedMultiplier; }
    }
    public static bool IsReplayFile(string? path) => !string.IsNullOrEmpty(path) &&
        Path.GetFileName(path).StartsWith("orbs_replay_", StringComparison.OrdinalIgnoreCase);

    // The writer can be behind its simulation clock, or flush just after an
    // empty tail read. Do not expire an idle combat ahead of unread records.
    internal static DateTime? GetDeliveredTime(long consumedBytes)
    {
        var current = Volatile.Read(ref _current);
        if (current == null) return null;
        var delivered = Volatile.Read(ref current._delivery);
        if (consumedBytes < delivered.FlushedBytes) return null;
        var now = current._anchor + current._clock.Elapsed * current._speedMultiplier;
        return now < delivered.Through ? now : delivered.Through;
    }

    internal static void PublishDelivery(DateTime through, long flushedBytes)
    {
        var current = Volatile.Read(ref _current);
        if (current != null) Volatile.Write(ref current._delivery, new DeliveryWatermark(through, flushedBytes));
    }

    public static LogReplayContext Begin(DateTime anchor, double speedMultiplier = 1)
    {
        ValidateSpeed(speedMultiplier);
        var context = new LogReplayContext(anchor, speedMultiplier);
        if (Interlocked.CompareExchange(ref _current, context, null) != null)
            throw new InvalidOperationException("A log replay is already active.");
        return context;
    }

    internal static void ValidateSpeed(double speedMultiplier)
    {
        if (!double.IsFinite(speedMultiplier) || speedMultiplier < 0.1 || speedMultiplier > 120)
            throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "Replay speed must be between 0.1 and 120 times real time.");
    }

    // Preparing the historical prefix must not consume the selected combat's playback time.
    public void Start() => _clock.Start();
    public void Dispose() => Interlocked.CompareExchange(ref _current, null, this);
}
