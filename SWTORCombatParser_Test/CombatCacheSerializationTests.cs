using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.AbilityInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatCacheSerializationTests
{
    private static readonly Type Manager = typeof(Combat).Assembly.GetType(
        "SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager", true);
    private static readonly FieldInfo CacheDirectory = Manager.GetField("_sessionCacheDirectory", BindingFlags.Static | BindingFlags.NonPublic);
    private string _directory;
    private object _previousDirectory;
    private LogState _previousState;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "orbs-cache-serialization-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _previousDirectory = CacheDirectory.GetValue(null);
        CacheDirectory.SetValue(null, _directory);
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.ClearState();
    }

    [TearDown]
    public void TearDown()
    {
        CacheDirectory.SetValue(null, _previousDirectory);
        CombatLogStateBuilder.CurrentState = _previousState;
        Directory.Delete(_directory, true);
    }

    [Test]
    public void WrittenCombatPreservesEntityDetailsAndMetricsWhenReloaded()
    {
        var logs = ExampleLogs();
        var original = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { original });
        var restoredLogs = LoadLogs(path);
        AssertLogDetails(logs, restoredLogs);
        var restored = (Combat)Manager.GetMethod("LoadCombat").Invoke(null, new object[] { path });
        var restoredPlayer = restored.CharacterParticipants[logs[0].Source.LogId];

        Assert.Multiple(() =>
        {
            Assert.That(restored.StartTime, Is.EqualTo(original.StartTime));
            Assert.That(restored.EndTime, Is.EqualTo(original.EndTime));
            Assert.That(restored.TotalDamage[restoredPlayer], Is.EqualTo(original.TotalDamage[logs[0].Source]));
            Assert.That(restored.TotalEffectiveDamage[restoredPlayer], Is.EqualTo(original.TotalEffectiveDamage[logs[0].Source]));
            Assert.That(restored.TotalThreat[restoredPlayer], Is.EqualTo(original.TotalThreat[logs[0].Source]));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LoadsExpandedAndCompactPayloadsFromTheCurrentLogState(bool compact)
    {
        var logs = ExampleLogs();
        var options = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = compact };
        var path = Path.Combine(_directory, compact ? "compact.json.br" : "legacy.json.br");
        using (var stream = File.Create(path))
        using (var compressed = new BrotliStream(stream, CompressionLevel.Fastest))
            JsonSerializer.Serialize(compressed, Payload(logs), options);

        var restored = LoadLogs(path);
        AssertLogDetails(logs, restored);
        Assert.Multiple(() =>
        {
            Assert.That(restored[0].Source, Is.SameAs(restored[1].Source));
            Assert.That(restored[0].Target, Is.Not.SameAs(restored[1].Target), "Different NPC instances share a type ID.");
            Assert.That(restored[0].ModifierEffectName, Is.EqualTo(logs[0].ModifierEffectName));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(4)]
    public void RejectsCachesWithoutASupportedOwnerAwareVersion(int version)
    {
        var path = WritePayload(Payload(ExampleLogs(), version));
        AssertInvalidCache(path);
    }

    [Test]
    public void EntityReferencesReduceRepeatedPayloadWithoutMergingNpcInstances()
    {
        var player = new Entity { Id = 101, LogId = 101, Name = "Shared Player", IsCharacter = true };
        var enemies = new[]
        {
            new Entity { Id = 201, LogId = 200, Name = "Same NPC type" },
            new Entity { Id = 202, LogId = 200, Name = "Same NPC type" }
        };
        var start = new DateTime(2026, 11, 7, 12, 0, 0);
        var logs = Enumerable.Range(0, 128).Select(index => Entry(index, start.AddMilliseconds(index),
            player, enemies[index % 2], EffectType.Apply, _7_0LogParsing._damageEffectId, "Attack", 100 + index)).ToList();
        var combat = new Combat { StartTime = start, EndTime = logs.Last().TimeStamp };
        foreach (var log in logs) combat.AllLogs[log.LogLineNumber] = log;
        var compactPath = WriteVersion3(logs);
        var version2Path = WritePayload(Payload(logs));
        var compactJson = ReadJson(compactPath);
        var version2Json = ReadJson(version2Path);
        using var document = JsonDocument.Parse(compactJson);
        var cachedRows = document.RootElement.GetProperty("Logs").EnumerateArray().ToArray();
        var restored = LoadLogs(compactPath);

        AssertLogDetails(logs, restored);
        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("Version").GetInt32(), Is.EqualTo(3));
            Assert.That(cachedRows.Count(row => row.GetProperty("SourceInfo").GetProperty("Entity").ValueKind == JsonValueKind.Object), Is.EqualTo(1));
            Assert.That(cachedRows.Count(row => row.GetProperty("TargetInfo").GetProperty("Entity").ValueKind == JsonValueKind.Object), Is.EqualTo(2));
            Assert.That(compactJson.Length, Is.LessThan(version2Json.Length * 0.9));
            Assert.That(restored[0].Source, Is.SameAs(restored[127].Source));
            Assert.That(restored[0].Target, Is.SameAs(restored[126].Target));
            Assert.That(restored[0].Target, Is.Not.SameAs(restored[1].Target));
        });
        TestContext.Progress.WriteLine($"Entity reference cache: JSON {version2Json.Length:N0} -> {compactJson.Length:N0} bytes; Brotli {new FileInfo(version2Path).Length:N0} -> {new FileInfo(compactPath).Length:N0} bytes.");
    }

    [Test]
    public void EntityDefinitionsLeaveOuterWriterFlushingToTheStreamingSerializer()
    {
        var converterType = Manager.GetNestedType("CachedEntityConverter", BindingFlags.NonPublic);
        var converter = (JsonConverter<Entity>)Activator.CreateInstance(converterType, true);
        using var output = new MemoryStream();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartArray();
        converter.Write(writer, new Entity { Id = 101, LogId = 101, Name = "Player", IsCharacter = true },
            new JsonSerializerOptions());

        // The outer serializer decides when to drain the stream from BytesPending.
        // A nested serializer Flush resets that counter and can grow its backing
        // pooled buffer to the size of an entire combat instead of one small chunk.
        Assert.Multiple(() =>
        {
            Assert.That(writer.BytesCommitted, Is.Zero);
            Assert.That(output.Length, Is.Zero);
            Assert.That(writer.BytesPending, Is.GreaterThan(0));
        });
        writer.WriteEndArray();
        writer.Flush();
    }

    [Test]
    public void LargeCombatRoundTripsAcrossCompressionBufferBoundaries()
    {
        var player = new Entity { Id = 101, LogId = 101, Name = "Player", IsCharacter = true };
        var enemy = new Entity { Id = 201, LogId = 200, Name = "Enemy" };
        var start = new DateTime(2026, 11, 7, 12, 0, 0);
        var logs = Enumerable.Range(0, 4096).Select(index => Entry(index, start.AddMilliseconds(index),
            player, enemy, EffectType.Apply, _7_0LogParsing._damageEffectId, "Attack", index + 0.12345)).ToList();
        var combat = new Combat { StartTime = start, EndTime = logs.Last().TimeStamp };
        foreach (var log in logs) combat.AllLogs[log.LogLineNumber] = log;

        var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { combat });
        using (var file = File.OpenRead(path))
        using (var compressed = new BrotliStream(file, CompressionMode.Decompress))
        using (var expanded = new MemoryStream())
        {
            compressed.CopyTo(expanded);
            Assert.That(expanded.Length, Is.GreaterThan(512 * 1024),
                "Exercise several read buffers and the final partial buffer.");
        }
        var restored = LoadLogs(path);
        AssertLogDetails(logs, restored);
        Assert.That(restored[0].Source, Is.SameAs(restored[^1].Source));
        Assert.That(restored[0].Target, Is.SameAs(restored[^1].Target));
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(100000)]
    public void RejectsEntityReferencesBeforeTheirDefinitionOrOutsideTheTable(int index)
    {
        var original = CombatIdentifier.GenerateCombatSnapshotFromLogs(ExampleLogs());
        var path = WriteVersion3(ExampleLogs());
        var payload = JsonNode.Parse(ReadJson(path));
        payload["Logs"][0]["SourceInfo"]["Entity"] = index;
        var error = AssertInvalidCache(WritePayload(payload));
        Assert.That(error.InnerException, Is.TypeOf<JsonException>());
        Assert.That(error.InnerException.Message, Does.Contain("invalid entity reference"));
    }

    [Test]
    public void ConcurrentFilesKeepIndependentEntityReferenceTables()
    {
        var paths = new string[8];
        var start = new DateTime(2026, 11, 7, 12, 0, 0);
        Parallel.For(0, paths.Length, index =>
        {
            var source = new Entity { Id = 100 + index, LogId = 100 + index, Name = "Player " + index, IsCharacter = true };
            var target = new Entity { Id = 200 + index, LogId = 200, Name = "NPC " + index };
            var combat = new Combat { StartTime = start, EndTime = start.AddSeconds(1) };
            combat.AllLogs[0] = Entry(0, start, source, target, EffectType.Apply, _7_0LogParsing._damageEffectId, "Attack", 100);
            combat.AllLogs[1] = Entry(1, start.AddSeconds(1), source, target, EffectType.Apply, _7_0LogParsing._damageEffectId, "Attack", 200);
            paths[index] = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { combat });
        });

        var loaded = new List<ParsedLogEntry>[paths.Length];
        Parallel.For(0, paths.Length, index => loaded[index] = LoadLogs(paths[index]));
        for (var index = 0; index < paths.Length; index++)
        {
            var restored = loaded[index];
            Assert.Multiple(() =>
            {
                Assert.That(restored[0].Source.Id, Is.EqualTo(100 + index));
                Assert.That(restored[0].Target.Id, Is.EqualTo(200 + index));
                Assert.That(restored[0].Source, Is.SameAs(restored[1].Source));
                Assert.That(restored[0].Target, Is.SameAs(restored[1].Target));
            });
        }
    }

    [Test]
    public void CacheFromAnotherLogStateCannotBorrowCurrentEntityHistories()
    {
        var logs = ExampleLogs();
        var originalPlayer = logs[0].Source;
        CombatLogStateBuilder.CurrentState.CurrentCharacterPositions[originalPlayer] = new PositionData();
        var combat = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { combat });
        CombatLogStateBuilder.ClearState();
        var otherPlayer = new Entity { Id = originalPlayer.Id, LogId = originalPlayer.LogId, Name = "Another log's player" };
        CombatLogStateBuilder.CurrentState.CurrentCharacterPositions[otherPlayer] = new PositionData();

        AssertInvalidCache(path);
        Assert.That(otherPlayer.Name, Is.EqualTo("Another log's player"));
        Assert.That(logs[0].Source, Is.SameAs(originalPlayer));
    }

    [Test]
    public void CombatRetainsItsOriginalOwnerIfTheLogChangesBeforeWriting()
    {
        var combat = CombatIdentifier.GenerateCombatSnapshotFromLogs(ExampleLogs());
        var originalOwner = typeof(Combat).GetProperty("CacheLogStateId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(combat);
        CombatLogStateBuilder.ClearState();
        var shell = (Combat)Manager.GetMethod("CreateCombatShell").Invoke(null, new object[] { combat });
        Assert.That(typeof(Combat).GetProperty("CacheLogStateId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shell),
            Is.EqualTo(originalOwner));
        var exception = Assert.Throws<TargetInvocationException>(() => Manager.GetMethod("CacheCombat").Invoke(null, new object[] { combat }));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
        Assert.That(Directory.GetFiles(_directory), Is.Empty);

        var row = new PastCombat();
        Assert.DoesNotThrow(() => row.CacheCombat(combat));
        Assert.That(row.Combat, Is.SameAs(combat), "A failed cache write keeps the existing combat usable.");
        Assert.That(row.CombatCachePath, Is.Null);
    }

    [TestCase("nullSnapshot")]
    [TestCase("missingLogs")]
    [TestCase("emptyLogs")]
    [TestCase("nullLog")]
    [TestCase("source")]
    [TestCase("target")]
    [TestCase("sourceEntity")]
    [TestCase("targetEntity")]
    [TestCase("effect")]
    [TestCase("value")]
    public void RejectsIncompleteCacheShapesBeforeRebuilding(string missing)
    {
        var logs = ExampleLogs();
        switch (missing)
        {
            case "emptyLogs": logs.Clear(); break;
            case "nullLog": logs[0] = null; break;
            case "source": logs[0].SourceInfo = null; break;
            case "target": logs[0].TargetInfo = null; break;
            case "sourceEntity": logs[0].SourceInfo.Entity = null; break;
            case "targetEntity": logs[0].TargetInfo.Entity = null; break;
            case "effect": logs[0].Effect = null; break;
            case "value": logs[0].Value = null; break;
        }
        var payload = Payload(logs);
        if (missing == "missingLogs") payload.Remove("Logs");
        var error = AssertInvalidCache(WritePayload(missing == "nullSnapshot" ? null : payload));
        if (missing != "nullSnapshot")
            Assert.That(error.Message, Does.Contain("missing required log data"));
    }

    [Test]
    public void NormalizationPreservesTrackedReferencesWithoutChangingTheirData()
    {
        var logs = ExampleLogs();
        var trackedPlayer = new Entity { Id = 101, LogId = 101, Name = "Original", IsCharacter = true };
        CombatLogStateBuilder.CurrentState.CurrentCharacterPositions[trackedPlayer] = new PositionData();
        Manager.GetMethod("NormalizeLogEntities").Invoke(null, new object[] { logs });

        Assert.Multiple(() =>
        {
            Assert.That(logs[0].Source, Is.SameAs(trackedPlayer));
            Assert.That(logs[1].Source, Is.SameAs(trackedPlayer));
            Assert.That(trackedPlayer.Name, Is.EqualTo("Original"));
            Assert.That(trackedPlayer.IsLocalPlayer, Is.False, "Serialized flags must not mutate the live state entity.");
        });
    }

    [Test]
    public void TypeOnlyEntitiesDoNotReconnectToTrackedNpcInstances()
    {
        var trackedNpc = new Entity { Id = 201, LogId = 200, Name = "Enemy" };
        var typeOnly = new Entity { LogId = 200, Name = "Enemy" };
        var typeMatchingInstanceId = new Entity { LogId = 201, Name = "Enemy" };
        CombatLogStateBuilder.CurrentState.CurrentCharacterPositions[trackedNpc] = new PositionData();
        var logs = ExampleLogs();
        logs[0].TargetInfo.Entity = typeOnly;
        logs[1].TargetInfo.Entity = typeMatchingInstanceId;

        Manager.GetMethod("NormalizeLogEntities").Invoke(null, new object[] { logs });
        Assert.Multiple(() =>
        {
            Assert.That(logs[0].Target, Is.SameAs(typeOnly));
            Assert.That(logs[1].Target, Is.SameAs(typeMatchingInstanceId));
        });
    }

    [Test]
    public void TrackedTargetLookupNeverFallsBackFromAnInstanceToItsTypeOrName()
    {
        var state = CombatLogStateBuilder.CurrentState;
        var npc = new Entity { Id = 201, LogId = 200, Name = "Enemy" };
        var target = new Entity { Id = 101, LogId = 101, Name = "Target", IsCharacter = true };
        var time = new DateTime(2026, 11, 7, 12, 0, 0);
        state.EnemyTargetsInfo[npc] = new();
        state.EnemyTargetsInfo[npc][time] = new EntityInfo { Entity = target };
        Assert.Multiple(() =>
        {
            Assert.That(state.GetEnemyTargetAtTime(new Entity { Id = 201, LogId = 200 }, time).Entity, Is.SameAs(target));
            Assert.That(state.GetEnemyTargetAtTime(new Entity { Id = 202, LogId = 200, Name = "Enemy" }, time).Entity, Is.SameAs(Entity.EmptyEntity));
            Assert.That(state.GetEnemyTargetAtTime(new Entity { LogId = 200, Name = "Enemy" }, time).Entity, Is.SameAs(Entity.EmptyEntity));
            Assert.That(state.GetEnemyTargetAtTime(new Entity { Name = "Enemy" }, time).Entity, Is.SameAs(Entity.EmptyEntity));
        });
    }

    [Test]
    public void EntityHistoryIndexesSupportBackgroundAddsDuringEnumeration()
    {
        var state = CombatLogStateBuilder.CurrentState;
        var first = new Entity { Id = 1, LogId = 1 };
        state.CurrentCharacterPositions[first] = new PositionData();
        state.PlayerDeathChangeInfo[first] = new();
        state.EnemyDeathChangeInfo[first] = new();
        using var positions = state.CurrentCharacterPositions.Keys.GetEnumerator();
        using var players = state.PlayerDeathChangeInfo.Keys.GetEnumerator();
        using var enemies = state.EnemyDeathChangeInfo.Keys.GetEnumerator();
        Assert.That(positions.MoveNext() && players.MoveNext() && enemies.MoveNext(), Is.True);

        // A cached-combat selection can be enumerating these indexes while the
        // live parser registers a new participant. Mutate after enumeration begins
        // to make the former Dictionary invalidation deterministic.
        Task.Run(() =>
        {
            var added = new Entity { Id = 2, LogId = 2 };
            state.CurrentCharacterPositions[added] = new PositionData();
            state.PlayerDeathChangeInfo[added] = new();
            state.EnemyDeathChangeInfo[added] = new();
        }).GetAwaiter().GetResult();

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(() => { while (positions.MoveNext()) { } });
            Assert.DoesNotThrow(() => { while (players.MoveNext()) { } });
            Assert.DoesNotThrow(() => { while (enemies.MoveNext()) { } });
            Assert.That(state.CurrentCharacterPositions, Has.Count.EqualTo(2));
            Assert.That(state.PlayerDeathChangeInfo, Has.Count.EqualTo(2));
            Assert.That(state.EnemyDeathChangeInfo, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void CacheReloadPreservesShieldingCalculatedFromSessionModifierHistory()
    {
        var previousAbilities = AbilityLoader.AbsorbAbilities;
        try
        {
            AbilityLoader.AbsorbAbilities = new Dictionary<double, AbilityInfo> { [42] = new() { name = "Barrier" } };
            var healer = new Entity { Name = "Healer", Id = 1, LogId = 1, IsCharacter = true };
            var tank = new Entity { Name = "Tank", Id = 2, LogId = 2, IsCharacter = true };
            var enemy = new Entity { Name = "Enemy", Id = 3, LogId = 3 };
            var start = new DateTime(2026, 11, 7, 12, 0, 0);
            SetModifier(42, "Barrier", healer, tank, start, start.AddSeconds(2));
            var apply = Entry(0, start, healer, tank, EffectType.Apply, 42, "Barrier", 0);
            var hit = Entry(1, start.AddSeconds(1), enemy, tank, EffectType.Apply, _7_0LogParsing._damageEffectId, "Attack", 100);
            hit.Value.MitigatedDblValue = 70;
            hit.Value.Modifier = new Value { ValueType = DamageType.absorbed, DblValue = 30 };
            var remove = Entry(2, start.AddSeconds(2), healer, tank, EffectType.Remove, 42, "Barrier", 0);
            var later = Entry(3, start.AddSeconds(3), healer, tank, EffectType.Apply, _7_0LogParsing._healEffectId, "Heal", 10);
            var original = CombatIdentifier.GenerateCombatSnapshotFromLogs(new[] { apply, hit, remove, later });
            Assert.That(original.TotalProvidedSheilding[healer], Is.EqualTo(30), "The original parsed combat must exercise an absorb.");

            var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { original });
            var restored = (Combat)Manager.GetMethod("LoadCombat").Invoke(null, new object[] { path });
            Assert.That(restored.TotalProvidedSheilding[restored.CharacterParticipants[healer.LogId]],
                Is.EqualTo(original.TotalProvidedSheilding[healer]));
        }
        finally { AbilityLoader.AbsorbAbilities = previousAbilities; }
    }

    [Test]
    public void CacheReloadPreservesDamageSavedDuringSessionCooldowns()
    {
        var tank = new Entity { Name = "Tank", Id = 2, LogId = 2, IsCharacter = true };
        var enemy = new Entity { Name = "Enemy", Id = 3, LogId = 3 };
        var start = new DateTime(2026, 11, 7, 12, 0, 0);
        SetModifier(2793700132388864, "Cooldown", tank, tank, start, start.AddSeconds(5));
        var logs = new[] { 1, 2, 3, 6, 7, 8 }.Select(seconds => Entry(seconds, start.AddSeconds(seconds),
            enemy, tank, EffectType.Apply, _7_0LogParsing._damageEffectId, "Attack", seconds < 5 ? 50 : 100)).ToArray();
        var original = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        Assert.That(original.AverageDamageSavedDuringCooldown[tank], Is.EqualTo(150), "The original combat must exercise a cooldown.");

        var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { original });
        var restored = (Combat)Manager.GetMethod("LoadCombat").Invoke(null, new object[] { path });
        Assert.That(restored.AverageDamageSavedDuringCooldown[restored.CharacterParticipants[tank.LogId]],
            Is.EqualTo(original.AverageDamageSavedDuringCooldown[tank]));
    }

    private static void SetModifier(ulong id, string name, Entity source, Entity target, DateTime start, DateTime end)
    {
        CombatLogStateBuilder.CurrentState.Modifiers[id] = new();
        CombatLogStateBuilder.CurrentState.Modifiers[id][Guid.NewGuid()] = new CombatModifier
        {
            EffectId = id, Name = name, EffectName = name, Source = source, Target = target,
            StartTime = start, StopTime = end, Complete = true
        };
    }

    private static ParsedLogEntry Entry(int line, DateTime time, Entity source, Entity target, EffectType type,
        ulong effect, string ability, double value) => new()
    {
        LogLineNumber = line, TimeStamp = time,
        SourceInfo = new EntityInfo { Entity = source, MaxHP = 10000 },
        TargetInfo = new EntityInfo { Entity = target, MaxHP = 10000 },
        Ability = ability,
        Effect = new Effect { EffectType = type, EffectId = effect, EffectName = ability },
        Value = new Value { DblValue = value, EffectiveDblValue = value, MitigatedDblValue = value }
    };

    private static List<ParsedLogEntry> LoadLogs(string path) =>
        (List<ParsedLogEntry>)Manager.GetMethod("LoadLogs").Invoke(null, new object[] { path });

    private static Guid CurrentStateId() => (Guid)typeof(LogState)
        .GetProperty("CacheIdentity", BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(CombatLogStateBuilder.CurrentState);

    private static Dictionary<string, object> Payload(List<ParsedLogEntry> logs, int version = 2) => new()
    {
        ["Version"] = version,
        ["LogStateId"] = CurrentStateId(),
        ["Logs"] = logs
    };

    private string WritePayload(object payload)
    {
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".json.br");
        using var file = File.Create(path);
        using var compressed = new BrotliStream(file, CompressionLevel.Fastest);
        JsonSerializer.Serialize(compressed, payload, new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true });
        return path;
    }

    private string WriteVersion3(List<ParsedLogEntry> logs)
    {
        var options = (JsonSerializerOptions)Manager.GetMethod("CreateReferenceOptions", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, null);
        var path = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".json.br");
        using var file = File.Create(path);
        using var compressed = new BrotliStream(file, CompressionLevel.Fastest);
        JsonSerializer.Serialize(compressed, Payload(logs, 3), options);
        return path;
    }

    [Test]
    public void BinaryCacheSharesRepeatedStringsWithoutLosingUnicodeOrNulls()
    {
        var logs = ExampleLogs();
        logs[0].Ability = logs[1].Ability = "Éclair 原力 ⚡";
        logs[0].LogLocation = null;
        logs[1].SourceInfo.Position = null;
        var combat = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { combat });
        var restored = LoadLogs(path);
        AssertLogDetails(logs, restored);
        Assert.That(restored[0].Ability, Is.SameAs(restored[1].Ability));
        Assert.That(path, Does.EndWith(".combat.br"));
    }

    [TestCase("header")]
    [TestCase("owner")]
    [TestCase("count")]
    [TestCase("reference")]
    [TestCase("footer")]
    public void BinaryCacheRejectsInvalidStructure(string field)
    {
        var combat = CombatIdentifier.GenerateCombatSnapshotFromLogs(ExampleLogs());
        var path = (string)Manager.GetMethod("CacheCombat").Invoke(null, new object[] { combat });
        byte[] bytes;
        using (var file = File.OpenRead(path))
        using (var compressed = new BrotliStream(file, CompressionMode.Decompress))
        using (var expanded = new MemoryStream()) { compressed.CopyTo(expanded); bytes = expanded.ToArray(); }
        var offset = field switch { "header" => 0, "owner" => 8, "count" => 24, "reference" => 40, _ => bytes.Length - 4 };
        bytes[offset] ^= 0x7F;
        if (field == "count") Array.Fill(bytes, (byte)0x7F, 24, 4);
        using (var file = File.Create(path))
        using (var compressed = new BrotliStream(file, CompressionLevel.Fastest)) compressed.Write(bytes);
        AssertInvalidCache(path);
    }

    private static string ReadJson(string path)
    {
        using var file = File.OpenRead(path);
        using var compressed = new BrotliStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(compressed);
        return reader.ReadToEnd();
    }

    private static InvalidDataException AssertInvalidCache(string path)
    {
        var exception = Assert.Throws<TargetInvocationException>(() => LoadLogs(path));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidDataException>());
        return (InvalidDataException)exception.InnerException;
    }

    private static void AssertLogDetails(IReadOnlyList<ParsedLogEntry> expected, IReadOnlyList<ParsedLogEntry> actual)
    {
        Assert.That(actual.Count, Is.EqualTo(expected.Count));
        // The compact serialization contains every writable property and public field
        // in the nested log graph; entity canonicalization should not alter those values.
        var options = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
        Assert.That(JsonSerializer.Serialize(actual, options), Is.EqualTo(JsonSerializer.Serialize(expected, options)));
    }

    private static List<ParsedLogEntry> ExampleLogs()
    {
        var player = new Entity { Name = "Player", Id = 101, LogId = 101, IsCharacter = true, IsLocalPlayer = true };
        var enemies = new[]
        {
            new Entity { Name = "Enemy", Id = 201, LogId = 200 },
            new Entity { Name = "Enemy", Id = 202, LogId = 200 }
        };
        return enemies.Select((enemy, index) => new ParsedLogEntry
        {
            Error = ErrorType.None,
            RaidLogId = 1234 + index,
            LogName = "test-combat.txt",
            LogBytes = 357,
            LogLineNumber = index + 5,
            LogLocation = "Test location",
            LogLocationId = 4567,
            LogDifficultyId = 8901,
            TimeStamp = new DateTime(2026, 11, 7, 12, 0, 0).AddSeconds(index),
            SecondsSinceCombatStart = index,
            SourceInfo = new EntityInfo
            {
                Entity = player,
                Position = new PositionData { X = 1.25f, Y = -2.5f, Z = 3.75f, Facing = 135 },
                CurrentHP = 40000,
                MaxHP = 50000,
                IsAlive = true,
                Class = new SWTORClass
                {
                    Name = "Sage", Discipline = "Telekinetics", DisciplineId = "123", IsRanged = true, Role = Role.DPS,
                    UniqueAbilities = new List<string> { "Disturbance" },
                    SpecialThreatAbilities = new List<Ability> { new() { Name = "Test", StaticThreat = true, Threatless = false } }
                }
            },
            TargetInfo = new EntityInfo
            {
                Entity = enemy,
                Position = new PositionData { X = 10, Y = 20, Z = 30, Facing = 90 },
                CurrentHP = 45000, MaxHP = 50000, IsAlive = true
            },
            Ability = "Test attack",
            AbilityId = 123456,
            Effect = new Effect
            {
                EffectType = EffectType.Apply, EffectName = "Damage", EffectId = _7_0LogParsing._damageEffectId, SecondEffectId = 654321
            },
            Value = new Value
            {
                DblValue = 1000 + index,
                EffectiveDblValue = 800 + index,
                MitigatedDblValue = 700 + index,
                StrValue = "1000*",
                DisplayValue = "1,000",
                WasCrit = true,
                ValueType = DamageType.energy,
                ValueTypeId = 836045448940874,
                ModifierType = "shield",
                ModifierDisplayValue = "100",
                Modifier = new Value { DblValue = 100, ValueType = DamageType.shield, DisplayValue = "100" }
            },
            Threat = 900 + index
        }).ToList();
    }
}
