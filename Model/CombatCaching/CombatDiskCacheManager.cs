using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Threading;

namespace SWTORCombatParser.Model.CombatCaching
{
    internal sealed class CombatCacheSnapshot
    {
        public int Version { get; set; }
        public Guid LogStateId { get; set; }
        public List<ParsedLogEntry>? Logs { get; set; }
    }

    internal static class CombatDiskCacheManager
    {
        private static readonly object _cacheLock = new();
        private static readonly RecentCombatCache _recentCombats = new();
        internal static void ClearRecentCombats() => _recentCombats.Clear();
        internal static void RememberCombat(string path, Combat combat)
        {
            if (!string.IsNullOrEmpty(path) && combat.CacheLogStateId == CombatLogStateBuilder.CurrentState.CacheIdentity)
                _recentCombats.Remember(path, combat);
        }
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            IncludeFields = true,
            // Source, Target and ModifierEffectName are derived from the stored
            // SourceInfo/TargetInfo, Ability and Effect. Avoid writing them twice.
            IgnoreReadOnlyProperties = true
        };

        private static string _sessionCacheDirectory;

        private static string CacheRootDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DubaTech",
                "SWTORCombatParser",
                "combat_cache");

        public static string CacheCombat(Combat combat)
        {
            using var timing = LogLoadDiagnostics.Measure("Write combat disk cache");
            var state = CombatLogStateBuilder.CurrentState;
            if (combat.CacheLogStateId != state.CacheIdentity)
                throw new InvalidDataException("The combat belongs to a different parsed log state and cannot be cached here.");
            EnsureCacheDirectory();

            var snapshot = new CombatCacheSnapshot
            {
                Version = 3,
                LogStateId = combat.CacheLogStateId,
                Logs = combat.AllLogs
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value)
                    .Where(log => log.Effect.EffectType != EffectType.AbsorbShield)
                    .ToList()
            };

            var cacheFile = Path.Combine(_sessionCacheDirectory,
                $"{combat.StartTime:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}.combat.br");
            var temporaryFile = cacheFile + ".tmp";
            try
            {
                using (var fileStream = File.Create(temporaryFile))
                using (var compressedStream = new BrotliStream(fileStream, CompressionLevel.Fastest))
                // Coalesce the record writer's small chunks for Brotli's fast compressor
                // without retaining a buffer as large as the whole combat.
                using (var bufferedStream = new BufferedStream(compressedStream, 1024 * 1024))
                    CombatCacheBinary.Write(bufferedStream, snapshot.LogStateId, snapshot.Logs);

                EnsureCurrentState(state);
                File.Move(temporaryFile, cacheFile);
                return cacheFile;
            }
            catch
            {
                try { File.Delete(temporaryFile); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                throw;
            }
        }

        public static Combat LoadCombat(string cacheFile)
        {
            using var timing = LogLoadDiagnostics.Measure("Rebuild combat from disk cache");
            var state = CombatLogStateBuilder.CurrentState;
            var resident = _recentCombats.Get(cacheFile, state.CacheIdentity);
            if (resident != null) { EnsureCurrentState(state); return resident; }
            var logs = LoadLogsForState(cacheFile, state);
            var combat = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs, combatEndUpdate: false);
            EnsureCurrentState(state);
            _recentCombats.Remember(cacheFile, combat);
            return combat;
        }

        public static List<ParsedLogEntry> LoadLogs(string cacheFile)
        {
            return LoadLogsForState(cacheFile, CombatLogStateBuilder.CurrentState);
        }

        private static List<ParsedLogEntry> LoadLogsForState(string cacheFile, LogState state)
        {
            var logs = NormalizeLogEntitiesForState(LoadDetachedLogs(cacheFile, state.CacheIdentity), state);
            EnsureCurrentState(state);
            return logs;
        }

        // Summary scans need the entities recorded in this file, without consulting the
        // active parser state or attaching the cache to a selected combat.
        internal static List<ParsedLogEntry> LoadDetachedLogs(string cacheFile, Guid owner, CancellationToken token = default)
        {
            using var timing = LogLoadDiagnostics.Measure("Read and deserialize combat cache");
            if (string.IsNullOrWhiteSpace(cacheFile))
                throw new FileNotFoundException("The combat cache path is missing.", cacheFile);

            using var fileStream = File.OpenRead(cacheFile);
            using var compressedStream = new BrotliStream(fileStream, CompressionMode.Decompress);
            using var bufferedStream = new BufferedStream(compressedStream, 256 * 1024);
            CombatCacheSnapshot? snapshot;
            try
            {
                snapshot = cacheFile.EndsWith(".combat.br", StringComparison.OrdinalIgnoreCase)
                    ? new CombatCacheSnapshot { Version = 3, LogStateId = owner,
                        Logs = CombatCacheBinary.Read(bufferedStream, owner, token) }
                    : JsonSerializer.Deserialize<CombatCacheSnapshot>(bufferedStream, CreateReferenceOptions());
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
            {
                // BrotliStream reports malformed compressed input as InvalidOperationException.
                throw new InvalidDataException("The combat cache contains invalid or incomplete log data.", exception);
            }

            // These files are disposable caches for one in-memory log state, not saved
            // sessions. Version 1 has no owner identity, so reconnecting it could silently
            // borrow another log's buffs/deaths/targets. Reload the source log instead.
            if (snapshot?.Version is not (2 or 3) || snapshot.LogStateId != owner)
                throw new InvalidDataException("The combat cache does not belong to the current parsed log state. Reload the source log.");
            if (snapshot.Logs == null || snapshot.Logs.Count == 0 || snapshot.Logs.Any(log =>
                    log == null || log.SourceInfo?.Entity == null || log.TargetInfo?.Entity == null ||
                    log.Effect == null || log.Value == null || log.TimeStamp == DateTime.MinValue))
                throw new InvalidDataException("The combat cache is missing required log data.");

            return snapshot.Logs;
        }

        private static JsonSerializerOptions CreateReferenceOptions()
        {
            // Each file owns its reference table. Sharing a converter across concurrent
            // combat writes, or retaining its entities after a load, would be unsafe.
            var options = new JsonSerializerOptions(_serializerOptions);
            options.Converters.Add(new CachedEntityConverter());
            return options;
        }

        private sealed class CachedEntityConverter : JsonConverter<Entity>
        {
            private readonly Dictionary<Entity, int> _writtenEntities = new(ReferenceEqualityComparer.Instance);
            private readonly List<Entity> _readEntities = new();

            public override Entity Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    if (!reader.TryGetInt32(out var index) || index < 0 || index >= _readEntities.Count)
                        throw new JsonException("The combat cache contains an invalid entity reference.");
                    return _readEntities[index];
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException("The combat cache contains invalid entity data.");

                // Version 2 already writes full objects, so it remains readable. In
                // version 3 the first occurrence defines a table entry and subsequent
                // occurrences use its index. Identity is by reference, never NPC type.
                var entity = JsonSerializer.Deserialize<Entity>(ref reader, _serializerOptions)
                    ?? throw new JsonException("The combat cache contains null entity data.");
                _readEntities.Add(entity);
                return entity;
            }

            public override void Write(Utf8JsonWriter writer, Entity entity, JsonSerializerOptions options)
            {
                if (_writtenEntities.TryGetValue(entity, out var index))
                {
                    writer.WriteNumberValue(index);
                    return;
                }

                _writtenEntities.Add(entity, _writtenEntities.Count);
                // Use the normal entity contract for definitions so all current and
                // future writable entity properties/public fields retain their values.
                // Serialize(writer, ...) would also Flush the outer writer. That resets
                // BytesPending without draining its streaming buffer, defeating the
                // serializer's flush threshold and retaining very large pooled arrays.
                JsonSerializer.SerializeToElement(entity, _serializerOptions).WriteTo(writer);
            }
        }

        private static void EnsureCurrentState(LogState state)
        {
            if (!ReferenceEquals(state, CombatLogStateBuilder.CurrentState))
                throw new InvalidDataException("The parsed log changed while accessing its combat cache. Select the combat again.");
        }

        public static Combat CreateCombatShell(Combat combat)
        {
            return new Combat
            {
                CacheLogStateId = combat.CacheLogStateId,
                StartTime = combat.StartTime,
                EndTime = combat.EndTime,
                ParentEncounter = combat.ParentEncounter,
                BossInfo = combat.BossInfo,
                EncounterBossDifficultyParts = combat.EncounterBossDifficultyParts,
                BossKillOverride = combat.BossKillOverride,
                DurationOverride = combat.DurationOverride,
                IsReplay = combat.IsReplay
            };
        }

        private static void EnsureCacheDirectory()
        {
            if (!string.IsNullOrEmpty(_sessionCacheDirectory))
                return;

            lock (_cacheLock)
            {
                if (!string.IsNullOrEmpty(_sessionCacheDirectory))
                    return;

                Directory.CreateDirectory(CacheRootDirectory);
                CleanupStaleCacheDirectories(CacheRootDirectory);

                _sessionCacheDirectory = Path.Combine(CacheRootDirectory,
                    $"session_{Process.GetCurrentProcess().Id}_{DateTime.UtcNow:yyyyMMddHHmmss}");
                Directory.CreateDirectory(_sessionCacheDirectory);
            }
        }

        private static void CleanupStaleCacheDirectories(string cacheRoot)
        {
            foreach (var directory in Directory.EnumerateDirectories(cacheRoot, "session_*") )
            {
                // Do not delete unrelated folders or a cache belonging to a live process.
                var parts = Path.GetFileName(directory).Split('_');
                if (parts.Length != 3 || parts[0] != "session" ||
                    !int.TryParse(parts[1], out var processId) || processId <= 0 ||
                    !DateTime.TryParseExact(parts[2], "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _))
                    continue;

                try
                {
                    using var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                        continue;
                }
                catch (ArgumentException)
                {
                    // No process with this ID remains.
                }
                catch (Exception)
                {
                    // If process status cannot be established, keep its cache.
                    continue;
                }

                try
                {
                    Directory.Delete(directory, true);
                }
                catch (Exception e)
                {
                    Logging.LogError("Failed to clean old combat cache directory: " + e.Message);
                }
            }
        }

        public static List<ParsedLogEntry> NormalizeLogEntities(List<ParsedLogEntry> logs)
        {
            var state = CombatLogStateBuilder.CurrentState;
            var normalized = NormalizeLogEntitiesForState(logs, state);
            EnsureCurrentState(state);
            return normalized;
        }

        private static List<ParsedLogEntry> NormalizeLogEntitiesForState(List<ParsedLogEntry> logs, LogState state)
        {
            var entitiesById = new Dictionary<(bool HasInstanceId, long Id), Entity>();
            var requiredIds = new HashSet<(bool HasInstanceId, long Id)>();
            foreach (var log in logs)
            {
                if (log.Source != null && EntityKey(log.Source).Id != 0)
                    requiredIds.Add(EntityKey(log.Source));
                if (log.Target != null && EntityKey(log.Target).Id != 0)
                    requiredIds.Add(EntityKey(log.Target));
            }

            // State dictionaries and modifiers use the parser's original references.
            // Reuse those references without changing their flags or replacing entities
            // already held by a resident combat's metrics. Positions usually resolves all
            // entities; only visit the larger target/modifier histories for missing IDs.
            if (requiredIds.Count != 0)
            {
                foreach (var entity in TrackedEntities(state))
                {
                    if (entity != null && requiredIds.Contains(EntityKey(entity)))
                        entitiesById.TryAdd(EntityKey(entity), entity);
                    if (entitiesById.Count == requiredIds.Count)
                        break;
                }
            }

            foreach (var log in logs)
            {
                log.SourceInfo.Entity = GetCanonicalEntity(log.SourceInfo.Entity, entitiesById);
                log.TargetInfo.Entity = GetCanonicalEntity(log.TargetInfo.Entity, entitiesById);
            }

            return logs;
        }

        private static IEnumerable<Entity> TrackedEntities(LogState state)
        {
            yield return state.LocalPlayer;
            foreach (var entity in state.CurrentCharacterPositions.Keys) yield return entity;
            foreach (var entity in state.PlayerDeathChangeInfo.Keys) yield return entity;
            foreach (var entity in state.EnemyDeathChangeInfo.Keys) yield return entity;
            foreach (var entity in state.PlayerClassChangeInfo.Keys) yield return entity;
            foreach (var entity in state.PlayerTargetsInfo.Keys) yield return entity;
            foreach (var entity in state.EnemyTargetsInfo.Keys) yield return entity;
            foreach (var history in state.PlayerTargetsInfo.Values)
                foreach (var target in history.Values) yield return target.Entity;
            foreach (var history in state.EnemyTargetsInfo.Values)
                foreach (var target in history.Values) yield return target.Entity;
            foreach (var instances in state.Modifiers.Values)
                foreach (var modifier in instances.Values)
                {
                    yield return modifier.Source;
                    yield return modifier.Target;
                }
        }

        private static (bool HasInstanceId, long Id) EntityKey(Entity entity) =>
            (entity.Id != 0, entity.Id != 0 ? entity.Id : entity.LogId);

        private static Entity GetCanonicalEntity(Entity entity, Dictionary<(bool HasInstanceId, long Id), Entity> entitiesById)
        {
            if (entity == null)
                return Entity.EmptyEntity;

            // NPCs can share a LogId (the NPC type) while having distinct Ids
            // (instances). Match the parser's instance identity where available.
            var key = EntityKey(entity);
            if (key.Id == 0)
                return entity;

            if (!entitiesById.TryGetValue(key, out var canonical))
            {
                entitiesById[key] = entity;
                return entity;
            }
            return canonical;
        }
    }
}
