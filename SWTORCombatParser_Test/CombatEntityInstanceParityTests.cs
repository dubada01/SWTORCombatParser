using System;
using System.Collections.Concurrent;
using System.Linq;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatEntityInstanceParityTests
{
    private LogState _previousState;

    [SetUp]
    public void Setup()
    {
        _previousState = CombatLogStateBuilder.CurrentState;
        CombatLogStateBuilder.ClearState();
    }

    [TearDown]
    public void Cleanup() => CombatLogStateBuilder.CurrentState = _previousState;

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(8)]
    public void SameTypeNpcInstancesKeepAllDamageAndThreatAcrossBatchPartitions(int batchSize)
    {
        var player = new Entity { Name = "Player", Id = 1, LogId = 1, IsCharacter = true, IsLocalPlayer = true };
        // A known discipline identifies this character as our player rather than
        // the existing fallback classification for an unknown PvP opponent.
        CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo[player] = new ConcurrentDictionary<DateTime, SWTORClass>
        {
            [new DateTime(2026, 11, 7)] = new SWTORClass { Discipline = "Arsenal", Role = Role.DPS }
        };
        var firstNpc = new Entity { Name = "Infantry Droid", Id = 101, LogId = 42 };
        var secondNpc = new Entity { Name = "Infantry Droid", Id = 102, LogId = 42 };
        var logs = new[]
        {
            Damage(1, player, firstNpc, 100, 100),
            Damage(2, secondNpc, player, 20),
            Damage(3, player, secondNpc, 200, 200),
            Damage(4, firstNpc, player, 10),
            Damage(5, secondNpc, player, 30),
            Damage(6, player, firstNpc, 50, 50),
            Damage(7, player, secondNpc, 70, 70),
            Damage(8, secondNpc, player, 40)
        };
        // A four-line bootstrap ends on firstNpc. The second instance already
        // dealt 20 damage and received 200 threat before becoming the final UI row.
        var whole = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var incremental = new Combat();
        foreach (var batch in logs.Chunk(batchSize))
            CombatIdentifier.GenerateCombatFromLogs(batch, combatShell: incremental);

        foreach (var combat in new[] { whole, incremental })
        {
            Assert.Multiple(() =>
            {
                Assert.That(combat.TotalDamage[player], Is.EqualTo(420));
                Assert.That(combat.TotalDamage[firstNpc], Is.EqualTo(10));
                Assert.That(combat.TotalDamage[secondNpc], Is.EqualTo(90));
                Assert.That(combat.TotalThreat[player], Is.EqualTo(420),
                    "Player threat must include both enemy instances, including the one hidden by the grouped UI row.");
                Assert.That(combat.PlayerThreatPerEnemy[firstNpc][player], Is.EqualTo(150));
                Assert.That(combat.PlayerThreatPerEnemy[secondNpc][player], Is.EqualTo(270));
                Assert.That(combat.TotalDamageTaken[player], Is.EqualTo(100));
                Assert.That(combat.TotalDamageTaken[firstNpc], Is.EqualTo(150));
                Assert.That(combat.TotalDamageTaken[secondNpc], Is.EqualTo(270));
                Assert.That(combat.OutgoingDamageLogs[secondNpc].Count, Is.EqualTo(3));
                Assert.That(combat.Targets.Count, Is.EqualTo(1), "Existing scope and target grouping remain unchanged.");
                Assert.That(combat.Targets[42], Is.SameAs(secondNpc));
                Assert.That(combat.AllEntities.Count, Is.EqualTo(2));
            });
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void LogBetweenSameTypeNpcInstancesIsCountedOnceInTheirSharedQueue(int batchSize)
    {
        var player = new Entity { Name = "Player", Id = 1, LogId = 1, IsCharacter = true };
        var firstNpc = new Entity { Name = "Infantry Droid", Id = 101, LogId = 42 };
        var secondNpc = new Entity { Name = "Infantry Droid", Id = 102, LogId = 42 };
        var logs = new[]
        {
            Damage(1, player, firstNpc, 100, 100),
            Damage(2, firstNpc, secondNpc, 25, 25),
            Damage(3, secondNpc, player, 20),
            Damage(4, player, secondNpc, 200, 200)
        };
        var whole = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var incremental = new Combat();
        foreach (var batch in logs.Chunk(batchSize))
            CombatIdentifier.GenerateCombatFromLogs(batch, combatShell: incremental);

        foreach (var combat in new[] { whole, incremental })
        {
            Assert.Multiple(() =>
            {
                Assert.That(combat.GetLogsInvolvingEntity(firstNpc).Count, Is.EqualTo(4),
                    "A shared NPC type queue must contain each physical record once.");
                Assert.That(combat.GetLogsInvolvingEntity(secondNpc).Count, Is.EqualTo(4));
                Assert.That(combat.TotalDamageTaken[firstNpc], Is.EqualTo(100));
                Assert.That(combat.TotalDamageTaken[secondNpc], Is.EqualTo(225));
                Assert.That(combat.TotalDamageTaken[player], Is.EqualTo(20));
                Assert.That(combat.TotalThreat[player], Is.EqualTo(300));
                Assert.That(combat.TotalThreat[firstNpc], Is.EqualTo(25));
                Assert.That(combat.TotalDamage[player], Is.EqualTo(300));
                Assert.That(combat.TotalDamage[secondNpc], Is.EqualTo(20));
            });
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(4)]
    public void AnonymousNpcWithAValidIdKeepsItsMetricsAcrossBatchPartitions(int batchSize)
    {
        var player = new Entity { Name = "Player", Id = 1, LogId = 1, IsCharacter = true };
        var anonymous = new Entity { Name = "", Id = 18998001835429, LogId = 3313047577821184 };
        var logs = new[]
        {
            Damage(1, anonymous, player, 50879),
            Damage(2, player, anonymous, 100, 100),
            Damage(3, anonymous, player, 60000),
            Damage(4, player, anonymous, 200, 200)
        };
        var whole = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var incremental = new Combat();
        foreach (var batch in logs.Chunk(batchSize))
            CombatIdentifier.GenerateCombatFromLogs(batch, combatShell: incremental);

        foreach (var combat in new[] { whole, incremental })
        {
            Assert.Multiple(() =>
            {
                Assert.That(combat.GetLogsInvolvingEntity(anonymous).Count, Is.EqualTo(4));
                Assert.That(combat.TotalDamage[anonymous], Is.EqualTo(110879));
                Assert.That(combat.TotalDamageTaken[player], Is.EqualTo(110879));
                Assert.That(combat.TotalDamageTaken[anonymous], Is.EqualTo(300));
                Assert.That(combat.TotalThreat[player], Is.EqualTo(300));
                Assert.That(combat.Targets[anonymous.LogId], Is.SameAs(anonymous));
            });
        }
    }

    private static ParsedLogEntry Damage(int line, Entity source, Entity target, double amount, double threat = 0) => new()
    {
        LogLineNumber = line,
        TimeStamp = new DateTime(2026, 11, 7, 12, 0, 0).AddSeconds(line),
        SourceInfo = new EntityInfo { Entity = source, MaxHP = 10000 },
        TargetInfo = new EntityInfo { Entity = target, MaxHP = 10000 },
        Ability = "Attack", AbilityId = 100,
        Effect = new Effect { EffectType = EffectType.Apply, EffectId = _7_0LogParsing._damageEffectId },
        Value = new Value { DblValue = amount, EffectiveDblValue = amount, MitigatedDblValue = amount },
        Threat = threat
    };
}
