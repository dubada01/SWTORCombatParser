using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.LogIndex;

/// <summary>
/// On-demand, newest-first indexing. The snapshot plus a durable append journal retain completed work
/// across cancellations and restarts. Unchanged files need only directory metadata, never a raw-log read.
/// </summary>
public sealed class CombatLogIndexService
{
    private const int FormatVersion = 1;
    private readonly string _cacheDirectory;
    private readonly string _metadataDirectory;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly ConcurrentDictionary<string, IReadOnlyList<IndexedCombatLog>> _snapshots = new(PathComparer);
    private readonly ConcurrentDictionary<string, bool> _pendingSaves = new(PathComparer);
    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static CombatLogIndexService Shared { get; } = new();

    public CombatLogIndexService(string? cacheDirectory = null, string? metadataDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DubaTech", "SWTORCombatParser", "log_index");
        _metadataDirectory = metadataDirectory ?? Path.Combine(AppContext.BaseDirectory, "DataStructures", "EncounterInfo");
    }

    public IReadOnlyList<IndexedCombatLog> GetCached(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory)) return Array.Empty<IndexedCombatLog>();
        var fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return _snapshots.GetOrAdd(fullDirectory, LoadCache);
    }

    public async Task<IReadOnlyList<IndexedCombatLog>> RefreshAsync(string directory,
        IProgress<LogIndexProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory)) return Array.Empty<IndexedCombatLog>();
        var fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => Refresh(fullDirectory, progress, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally { _refreshGate.Release(); }
    }

    private IReadOnlyList<IndexedCombatLog> Refresh(string directory, IProgress<LogIndexProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        // A missing or temporarily disconnected log folder must not erase the last good cache.
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The combat log folder is unavailable: " + directory);
        var current = GetCached(directory);
        var results = current.ToDictionary(log => log.FilePath, PathComparer);
        var files = new DirectoryInfo(directory).EnumerateFiles()
            .Where(f => f.Extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(CombatLogMetadataScanner.FileStart).ThenByDescending(f => f.LastWriteTimeUtc).ToArray();
        CombatLogMetadataScanner? scanner = null;
        FileStream? journal = null;
        int indexed = 0, reused = 0, failed = 0, processed = 0;
        bool changed = false, firstUpdate = true, saveFailed = false;
        var reportClock = Stopwatch.StartNew();
        try
        {
            progress?.Report(new LogIndexProgress(0, files.Length, 0, 0, 0, "Checking for new, changed, and deleted logs…"));
            token.ThrowIfCancellationRequested();
            var present = files.Select(file => file.FullName).ToHashSet(PathComparer);
            foreach (var removed in results.Keys.Where(path => !present.Contains(path)).ToArray())
            {
                results.Remove(removed);
                Persist(new CacheUpdate { Directory = directory, RemovedPath = removed });
                changed = true;
            }
            if (changed) Report("Removed deleted logs from the library.", true);
            token.ThrowIfCancellationRequested();
            // Load parser metadata only if some raw file actually needs scanning. Configuration errors
            // belong to the refresh, not to individual logs, and must not remove last-good rows.
            if (files.Any(file => !results.TryGetValue(file.FullName, out var cached) ||
                cached.FileSize != file.Length || cached.LastWriteTimeUtc != file.LastWriteTimeUtc))
                scanner = new CombatLogMetadataScanner(_metadataDirectory);
            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (results.TryGetValue(file.FullName, out var cached) && cached.FileSize == file.Length &&
                        cached.LastWriteTimeUtc == file.LastWriteTimeUtc)
                    {
                        reused++;
                    }
                    else
                    {
                        var indexedLog = scanner!.Scan(file, token);
                        results[file.FullName] = indexedLog;
                        // Flush each completed file before announcing it. An interrupted first scan resumes here.
                        Persist(new CacheUpdate { Directory = directory, Log = indexedLog });
                        indexed++;
                        changed = true;
                    }
                }
                catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
                {
                    if (results.Remove(file.FullName))
                    {
                        Persist(new CacheUpdate { Directory = directory, RemovedPath = file.FullName });
                        changed = true;
                    }
                    failed++;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Keep last-good metadata for temporarily unreadable files; their old stamp causes a retry next time.
                    failed++;
                }
                processed++;
                Report(file.Name);
            }
            token.ThrowIfCancellationRequested();
            Publish();
            journal?.Dispose();
            journal = null;
            // Compact once per completed refresh, not once per file. No-op refreshes don't rewrite the index.
            if (_pendingSaves.ContainsKey(directory) || File.Exists(JournalPath(directory)))
            {
                try
                {
                    SaveCache(directory, current);
                    if (File.Exists(JournalPath(directory))) File.Delete(JournalPath(directory));
                    _pendingSaves.TryRemove(directory, out _);
                    saveFailed = false;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { saveFailed = true; }
            }
            var message = failed == 0 ? "Saved library is up to date." : $"Library ready; {failed} unavailable files will be checked next time.";
            if (saveFailed) message += " Some metadata could not be saved; refresh to retry.";
            progress?.Report(new LogIndexProgress(processed, files.Length, indexed, reused, failed, message, current));
            return current;
        }
        finally
        {
            journal?.Dispose();
            Publish();
        }

        void Persist(CacheUpdate update)
        {
            _pendingSaves[directory] = true;
            try
            {
                Directory.CreateDirectory(_cacheDirectory);
                journal ??= new FileStream(JournalPath(directory), FileMode.Append, FileAccess.Write, FileShare.Read);
                // A leading newline isolates a torn record left by an interrupted process.
                var bytes = Encoding.UTF8.GetBytes("\n" + JsonSerializer.Serialize(update) + "\n");
                journal.Write(bytes);
                journal.Flush(flushToDisk: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                saveFailed = true;
                journal?.Dispose();
                journal = null;
            }
        }

        void Publish()
        {
            if (!changed) return;
            current = results.Values.OrderByDescending(log => log.StartTime).ThenBy(log => log.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
            _snapshots[directory] = current;
            changed = false;
        }

        void Report(string message, bool force = false)
        {
            if (!force && !(changed && firstUpdate) && reportClock.ElapsedMilliseconds < 250) return;
            var hasNewResults = changed;
            Publish();
            if (hasNewResults) firstUpdate = false;
            if (saveFailed) message += " · Metadata could not be saved to disk.";
            progress?.Report(new LogIndexProgress(processed, files.Length, indexed, reused, failed, message,
                hasNewResults ? current : null));
            reportClock.Restart();
        }
    }

    private IReadOnlyList<IndexedCombatLog> LoadCache(string directory)
    {
        var logs = new Dictionary<string, IndexedCombatLog>(PathComparer);
        try
        {
            using var stream = File.OpenRead(CachePath(directory));
            var cache = JsonSerializer.Deserialize<CacheSnapshot>(stream);
            // Build IDs and catalog changes do not discard metadata for unchanged source files.
            if (cache?.Version == FormatVersion && cache.Logs != null && PathComparer.Equals(cache.Directory, directory))
                foreach (var log in cache.Logs.Where(log => IsValid(log, directory))) logs[log.FilePath] = log;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        { /* Missing/corrupt snapshots may still have a valid journal. */ }
        try
        {
            using var stream = new FileStream(JournalPath(directory), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var update = JsonSerializer.Deserialize<CacheUpdate>(line);
                    if (update?.Version != FormatVersion || !PathComparer.Equals(update.Directory, directory)) continue;
                    if (IsValid(update.Log, directory)) logs[update.Log!.FilePath] = update.Log;
                    else if (IsLocalPath(update.RemovedPath, directory)) logs.Remove(update.RemovedPath!);
                }
                catch (JsonException) { /* Ignore a torn record; retry that file on the next refresh. */ }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        return logs.Values.OrderByDescending(log => log.StartTime).ThenBy(log => log.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool IsValid(IndexedCombatLog? log, string directory) => log != null && log.FileName != null &&
        IsLocalPath(log.FilePath, directory) && log.Players != null && log.Players.All(p => p != null) && log.Encounters != null &&
        log.Encounters.All(e => e != null && e.Players != null && e.Players.All(p => p != null) &&
            e.ActivityType != null && e.EncounterName != null && e.BossName != null && e.Difficulty != null && e.Outcome != null);

    private static bool IsLocalPath(string? path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { return PathComparer.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), directory); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException) { return false; }
    }

    private void SaveCache(string directory, IReadOnlyList<IndexedCombatLog> logs)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var destination = CachePath(directory);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, new CacheSnapshot
                {
                    Directory = directory, Logs = logs.ToList()
                });
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private string CachePath(string directory)
    {
        var key = OperatingSystem.IsWindows() ? directory.ToUpperInvariant() : directory;
        return Path.Combine(_cacheDirectory, SWTORCombatParser.Utilities.StableHash.Sha256(key) + ".json");
    }

    private string JournalPath(string directory) => CachePath(directory) + ".updates";

    public sealed class CacheSnapshot
    {
        public int Version { get; set; } = FormatVersion;
        public string Directory { get; set; } = "";
        public List<IndexedCombatLog> Logs { get; set; } = new();
    }

    public sealed class CacheUpdate
    {
        public int Version { get; set; } = FormatVersion;
        public string Directory { get; set; } = "";
        public IndexedCombatLog? Log { get; set; }
        public string? RemovedPath { get; set; }
    }
}
