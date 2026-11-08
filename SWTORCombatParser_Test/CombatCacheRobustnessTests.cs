using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser_Test;

public class CombatCacheRobustnessTests
{
    [Test]
    public void CleanupLeavesLiveAndUnrecognizedDirectoriesAlone()
    {
        var root = Path.Combine(Path.GetTempPath(), "orbs-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var live = Path.Combine(root, $"session_{Process.GetCurrentProcess().Id}_20240101000000");
            var stale = Path.Combine(root, "session_2147483647_20240101000000");
            var unknown = Path.Combine(root, "other-data");
            var malformed = Path.Combine(root, "session_unrecognized");
            foreach (var path in new[] { live, stale, unknown, malformed })
                Directory.CreateDirectory(path);

            var manager = typeof(Combat).Assembly.GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager", true);
            manager.GetMethod("CleanupStaleCacheDirectories", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { root });

            Assert.That(Directory.Exists(live), Is.True);
            Assert.That(Directory.Exists(stale), Is.False);
            Assert.That(Directory.Exists(unknown), Is.True);
            Assert.That(Directory.Exists(malformed), Is.True);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public void PreexistingParticipantWithoutLogsDoesNotEraseOtherEntityLogs()
    {
        var combat = new Combat();
        var missing = new Entity { Name = "absent", LogId = 999, IsCharacter = true };
        combat.CharacterParticipants[missing.LogId] = missing;
        var source = new Entity { Name = "source", LogId = 1 };
        var target = new Entity { Name = "target", LogId = 2 };
        var log = new ParsedLogEntry
        {
            TimeStamp = new DateTime(2024, 1, 1),
            SourceInfo = new EntityInfo { Entity = source },
            TargetInfo = new EntityInfo { Entity = target },
            Effect = new Effect(),
            Value = new Value()
        };

        CombatIdentifier.GenerateCombatFromLogs(new[] { log }, combatShell: combat);

        Assert.That(combat.GetLogsInvolvingEntity(source), Does.Contain(log));
        Assert.That(combat.GetLogsInvolvingEntity(target), Does.Contain(log));
    }

    [Test]
    public void CachedEntitiesKeepDistinctInstancesOfSameNpcType()
    {
        var first = new Entity { Name = "NPC", LogId = 42, Id = 101 };
        var firstCopy = new Entity { Name = "NPC", LogId = 42, Id = 101 };
        var second = new Entity { Name = "NPC", LogId = 42, Id = 102 };
        var fallback = new Entity { Name = "Unknown", LogId = 101 };
        var logs = new List<ParsedLogEntry>
        {
            new() { SourceInfo = new EntityInfo { Entity = first }, TargetInfo = new EntityInfo { Entity = second } },
            new() { SourceInfo = new EntityInfo { Entity = firstCopy }, TargetInfo = new EntityInfo { Entity = fallback } }
        };
        var manager = typeof(Combat).Assembly.GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager", true);
        manager.GetMethod("NormalizeLogEntities", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { logs });

        Assert.That(logs[0].Source, Is.SameAs(logs[1].Source));
        Assert.That(logs[0].Source, Is.Not.SameAs(logs[0].Target));
        Assert.That(logs[0].Source, Is.Not.SameAs(logs[1].Target));
    }

    [Test]
    [NonParallelizable]
    public void LogAvailabilityIgnoresUnrelatedFilesAndMatchesTextExtensionCaseInsensitively()
    {
        var root = Path.Combine(Path.GetTempPath(), "orbs-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousPath = CombatLogLoader.LoggingPath;
        try
        {
            CombatLogLoader.LoggingPath = root;
            File.WriteAllText(Path.Combine(root, "notes.json"), "{}");
            Assert.That(CombatLogLoader.CheckIfCombatLogsArePresent(), Is.False);
            var logPath = Path.Combine(root, "combat.TXT");
            File.WriteAllText(logPath, "");
            Assert.That(CombatLogLoader.CheckIfCombatLogsArePresent(), Is.True);
            Assert.That(CombatLogLoader.GetMostRecentLogPath(), Is.EqualTo(logPath));
        }
        finally
        {
            CombatLogLoader.LoggingPath = previousPath;
            Directory.Delete(root, true);
        }
    }

    [Test]
    [NonParallelizable]
    public void FailedCacheWriteKeepsCombatInMemory()
    {
        var manager = typeof(Combat).Assembly.GetType("SWTORCombatParser.Model.CombatCaching.CombatDiskCacheManager", true);
        var directoryField = manager.GetField("_sessionCacheDirectory", BindingFlags.NonPublic | BindingFlags.Static);
        var previousDirectory = directoryField.GetValue(null);
        var missingDirectory = Path.Combine(Path.GetTempPath(), "orbs-missing-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            directoryField.SetValue(null, missingDirectory);
            var combat = new Combat { StartTime = DateTime.Today, EndTime = DateTime.Today, BossInfo = new BossInfo() };
            var entry = new PastCombat();
            entry.CacheCombat(combat);
            Assert.That(entry.Combat, Is.SameAs(combat));
            Assert.That(entry.CombatCachePath, Is.Null);
        }
        finally
        {
            directoryField.SetValue(null, previousDirectory);
        }
    }
}
