using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.LogParsing;

public sealed record LogReplayRequest(string FilePath, DateTime? From = null, DateTime? Through = null,
    bool SimulatePartialWrite = false);
public sealed record LogReplayProgress(TimeSpan Elapsed, TimeSpan Duration, long WrittenLines);

/// <summary>
/// Integrated version of the standalone LogReplayer: sequential disk spooling, original event spacing,
/// midnight-aware timestamps, cancellation, and an optional split-write test against the real tail reader.
/// </summary>
public sealed class CombatLogReplay : IDisposable
{
    private readonly string _directory;
    private readonly string _spool;
    private readonly LogReplayRequest _request;
    private long _playbackOffset;
    private static readonly Encoding OutputEncoding = new UTF8Encoding(true);
    public string OutputPath { get; }
    public DateTime SourceStart { get; private set; }
    public DateTime PlaybackStart { get; private set; }
    public DateTime PlaybackEnd { get; private set; }
    public TimeSpan Duration => PlaybackEnd - PlaybackStart;

    private CombatLogReplay(LogReplayRequest request)
    {
        _request = request;
        _directory = Path.Combine(Path.GetTempPath(), "Orbs", "replay_" + Guid.NewGuid().ToString("N"));
        _spool = Path.Combine(_directory, "events.bin");
        OutputPath = Path.Combine(_directory, "orbs_replay_" + Guid.NewGuid().ToString("N") + ".txt");
    }

    public static Task<CombatLogReplay> PrepareAsync(LogReplayRequest request, CancellationToken token = default) => Task.Run(() =>
    {
        if (request.From.HasValue && request.Through.HasValue && request.Through < request.From)
            throw new ArgumentException("The replay end must follow its start.");
        var replay = new CombatLogReplay(request);
        try { replay.Prepare(token); return replay; }
        catch { replay.Dispose(); throw; }
    }, token);

    private void Prepare(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var source = new FileInfo(_request.FilePath);
        Directory.CreateDirectory(_directory);
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var input = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            65536, FileOptions.SequentialScan);
        // Snapshot the byte boundary so a real writer cannot extend this replay indefinitely.
        using var bounded = new BoundedInput(input, input.Length);
        using var reader = new StreamReader(bounded, Encoding.GetEncoding(1252), true, 65536);
        using var writer = new BinaryWriter(File.Create(_spool), OutputEncoding);
        var fileStart = GetFileStart(source);
        var date = fileStart.Date;
        DateTime? latest = null;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            token.ThrowIfCancellationRequested();
            if (!TryReadClock(line, out var clock)) continue;
            var timestamp = date + clock;
            if (timestamp < (latest ?? fileStart).AddHours(-12)) { date = date.AddDays(1); timestamp = date + clock; }
            latest = !latest.HasValue || timestamp > latest ? timestamp : latest;
            if (_request.Through.HasValue && timestamp > _request.Through.Value) break;
            if (SourceStart == default) SourceStart = timestamp;
            if (!_request.From.HasValue || timestamp >= _request.From.Value)
            {
                if (PlaybackStart == default) { PlaybackStart = timestamp; _playbackOffset = writer.BaseStream.Position; }
                if (timestamp > PlaybackEnd) PlaybackEnd = timestamp;
            }
            writer.Write(timestamp.Ticks);
            writer.Write(line);
        }
        if (PlaybackStart == default) throw new InvalidDataException("No timestamped events remain in the selected combat. Refresh the library and try again.");
    }

    public DateTime ParseDate(DateTime anchor) => anchor + (SourceStart - PlaybackStart);

    public Task WritePreludeAsync(DateTime anchor, CancellationToken token = default) => Task.Run(() =>
    {
        using var events = OpenEvents();
        using (var writer = new StreamWriter(new FileStream(OutputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), OutputEncoding))
        {
            while (TryRead(events, out var timestamp, out var line))
            {
                token.ThrowIfCancellationRequested();
                if (timestamp >= PlaybackStart) break;
                writer.WriteLine(Rebase(line, anchor + (timestamp - PlaybackStart)));
            }
        }
        // The existing historical parser seeds its calendar date from this stamp.
        File.SetLastWriteTime(OutputPath, ParseDate(anchor));
    }, token);

    public async Task PlayAsync(DateTime anchor, IProgress<LogReplayProgress>? progress = null, CancellationToken token = default)
    {
        using var events = OpenEvents();
        events.BaseStream.Position = _playbackOffset;
        using var writer = new StreamWriter(new FileStream(OutputPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite,
            65536, FileOptions.Asynchronous), OutputEncoding, 65536);
        var clock = Stopwatch.StartNew();
        var nextReport = TimeSpan.Zero;
        var lastFlush = TimeSpan.Zero;
        long written = 0;
        bool split = false;
        while (TryRead(events, out var timestamp, out var line))
        {
            token.ThrowIfCancellationRequested();
            if (timestamp < PlaybackStart) continue;
            var due = timestamp - PlaybackStart;
            while (clock.Elapsed < due)
            {
                await writer.FlushAsync(token).ConfigureAwait(false);
                Report();
                var delay = due - clock.Elapsed;
                if (delay > TimeSpan.Zero) await Task.Delay(delay < TimeSpan.FromMilliseconds(100) ? delay : TimeSpan.FromMilliseconds(100), token).ConfigureAwait(false);
            }
            var rebased = Rebase(line, anchor + due);
            if (_request.SimulatePartialWrite && !split)
            {
                split = true;
                await writer.WriteAsync(rebased.AsMemory(0, rebased.Length / 2), token).ConfigureAwait(false);
                await writer.FlushAsync(token).ConfigureAwait(false);
                await Task.Delay(350, token).ConfigureAwait(false);
                await writer.WriteLineAsync(rebased.AsMemory(rebased.Length / 2), token).ConfigureAwait(false);
            }
            else await writer.WriteLineAsync(rebased.AsMemory(), token).ConfigureAwait(false);
            written++;
            if (clock.Elapsed - lastFlush >= TimeSpan.FromMilliseconds(100))
            {
                await writer.FlushAsync(token).ConfigureAwait(false);
                lastFlush = clock.Elapsed;
            }
            Report();
        }
        await writer.FlushAsync(token).ConfigureAwait(false);
        progress?.Report(new LogReplayProgress(Duration, Duration, written));

        void Report()
        {
            if (clock.Elapsed < nextReport) return;
            nextReport = clock.Elapsed + TimeSpan.FromMilliseconds(250);
            progress?.Report(new LogReplayProgress(clock.Elapsed < Duration ? clock.Elapsed : Duration, Duration, written));
        }
    }

    private BinaryReader OpenEvents() => new(File.OpenRead(_spool), OutputEncoding);
    private static bool TryRead(BinaryReader events, out DateTime timestamp, out string line)
    {
        if (events.BaseStream.Position == events.BaseStream.Length) { timestamp = default; line = ""; return false; }
        timestamp = new DateTime(events.ReadInt64());
        line = events.ReadString();
        return true;
    }
    private static bool TryReadClock(string line, out TimeSpan clock)
    {
        clock = default;
        return line.Length >= 14 && line[0] == '[' && line[13] == ']' &&
            TimeSpan.TryParseExact(line.AsSpan(1, 12), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture, out clock);
    }
    private static string Rebase(string line, DateTime timestamp) => "[" + timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "]" + line[14..];
    private static DateTime GetFileStart(FileInfo file)
    {
        var name = file.Name;
        return name.StartsWith("combat_", StringComparison.OrdinalIgnoreCase) && name.Length >= 26 &&
            DateTime.TryParseExact(name.AsSpan(7, 19), "yyyy-MM-dd_HH_mm_ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time : file.LastWriteTime.Date;
    }
    public void Dispose()
    {
        // Only these two files belong to this instance; never recursively delete a computed path.
        try
        {
            if (File.Exists(OutputPath)) File.Delete(OutputPath);
            if (File.Exists(_spool)) File.Delete(_spool);
            if (Directory.Exists(_directory)) Directory.Delete(_directory, false);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class BoundedInput(Stream source, long length) : Stream
    {
        public override int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, (int)Math.Min(count, Math.Max(0, length - source.Position)));
        public override int Read(Span<byte> buffer) => source.Read(buffer[..(int)Math.Min(buffer.Length, Math.Max(0, length - source.Position))]);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => source.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
