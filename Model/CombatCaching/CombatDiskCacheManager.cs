using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Globalization;

namespace SWTORCombatParser.Model.CombatCaching
{
    internal sealed class CombatCacheSnapshot
    {
        public int Version { get; set; } = 1;
        public List<ParsedLogEntry> Logs { get; set; } = new();
    }

    internal static class CombatDiskCacheManager
    {
        private static readonly object _cacheLock = new();
        private static readonly JsonSerializerOptions _serializerOptions = new()
        {
            IncludeFields = true
        };

        private static string _sessionCacheDirectory;

        private static string CacheRootDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DubaTech",
                "SWTORCombatParser",
                "combat_cache");

        public static string CacheCombat(Combat combat)
        {
            EnsureCacheDirectory();

            var snapshot = new CombatCacheSnapshot
            {
                Logs = combat.AllLogs
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp => kvp.Value)
                    .Where(log => log.Effect.EffectType != EffectType.AbsorbShield)
                    .ToList()
            };

            var cacheFile = Path.Combine(_sessionCacheDirectory,
                $"{combat.StartTime:yyyyMMdd_HHmmss_fffffff}_{Guid.NewGuid():N}.json.br");

            using var fileStream = File.Create(cacheFile);
            using var compressedStream = new BrotliStream(fileStream, CompressionLevel.Fastest);
            JsonSerializer.Serialize(compressedStream, snapshot, _serializerOptions);
            return cacheFile;
        }

        public static Combat LoadCombat(string cacheFile)
        {
            var logs = LoadLogs(cacheFile);
            if (logs.Count == 0)
                return new Combat();
            return CombatIdentifier.GenerateCombatSnapshotFromLogs(logs, combatEndUpdate: false);
        }

        public static List<ParsedLogEntry> LoadLogs(string cacheFile)
        {
            if (string.IsNullOrEmpty(cacheFile) || !File.Exists(cacheFile))
                return new List<ParsedLogEntry>();

            using var fileStream = File.OpenRead(cacheFile);
            using var compressedStream = new BrotliStream(fileStream, CompressionMode.Decompress);
            var snapshot = JsonSerializer.Deserialize<CombatCacheSnapshot>(compressedStream, _serializerOptions);
            var logs = snapshot?.Logs ?? new List<ParsedLogEntry>();
            return NormalizeLogEntities(logs);
        }

        public static Combat CreateCombatShell(Combat combat)
        {
            return new Combat
            {
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
            var entitiesById = new Dictionary<(bool HasInstanceId, long Id), Entity>();

            foreach (var log in logs)
            {
                log.SourceInfo.Entity = GetCanonicalEntity(log.SourceInfo.Entity, entitiesById);
                log.TargetInfo.Entity = GetCanonicalEntity(log.TargetInfo.Entity, entitiesById);
            }

            return logs;
        }

        private static Entity GetCanonicalEntity(Entity entity, Dictionary<(bool HasInstanceId, long Id), Entity> entitiesById)
        {
            if (entity == null)
                return Entity.EmptyEntity;

            // NPCs can share a LogId (the NPC type) while having distinct Ids
            // (instances). Match the parser's instance identity where available.
            var id = entity.Id != 0 ? entity.Id : entity.LogId;
            if (id == 0)
                return entity;
            var key = (entity.Id != 0, id);

            if (!entitiesById.TryGetValue(key, out var canonical))
            {
                canonical = new Entity
                {
                    Name = entity.Name,
                    Id = entity.Id,
                    LogId = entity.LogId,
                    IsBoss = entity.IsBoss,
                    IsCharacter = entity.IsCharacter,
                    IsCompanion = entity.IsCompanion,
                    IsLocalPlayer = entity.IsLocalPlayer
                };
                entitiesById[key] = canonical;
                return canonical;
            }

            canonical.IsBoss |= entity.IsBoss;
            canonical.IsCharacter |= entity.IsCharacter;
            canonical.IsCompanion |= entity.IsCompanion;
            canonical.IsLocalPlayer |= entity.IsLocalPlayer;
            if (string.IsNullOrEmpty(canonical.Name) && !string.IsNullOrEmpty(entity.Name))
                canonical.Name = entity.Name;
            if (canonical.Id == 0 && entity.Id != 0)
                canonical.Id = entity.Id;
            if (canonical.LogId == 0 && entity.LogId != 0)
                canonical.LogId = entity.LogId;
            return canonical;
        }
    }
}
