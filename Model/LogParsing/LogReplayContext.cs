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
    private readonly Stopwatch _clock = new();
    private LogReplayContext(DateTime anchor) => _anchor = anchor;
    public static bool IsActive => Volatile.Read(ref _current) != null;
    public static DateTime? CurrentTime
    {
        get { var current = Volatile.Read(ref _current); return current == null ? null : current._anchor + current._clock.Elapsed; }
    }
    public static bool IsReplayFile(string? path) => !string.IsNullOrEmpty(path) &&
        Path.GetFileName(path).StartsWith("orbs_replay_", StringComparison.OrdinalIgnoreCase);

    public static LogReplayContext Begin(DateTime anchor)
    {
        var context = new LogReplayContext(anchor);
        if (Interlocked.CompareExchange(ref _current, context, null) != null)
            throw new InvalidOperationException("A log replay is already active.");
        return context;
    }

    // Preparing the historical prefix must not consume the selected combat's playback time.
    public void Start() => _clock.Start();
    public void Dispose() => Interlocked.CompareExchange(ref _current, null, this);
}
