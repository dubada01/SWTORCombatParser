using System;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test;

[NonParallelizable]
public class CombatMetricParityTests
{
    [Test]
    public void LiveUpdatesMatchSnapshotForDamageHealingAndThreat()
    {
        CombatLogStateBuilder.ClearState();
        var player = new Entity { Name = "Player", Id = 1, LogId = 1, IsCharacter = true };
        var enemy = new Entity { Name = "Enemy", Id = 2, LogId = 2 };
        var start = new DateTime(2024, 1, 1, 12, 0, 0);
        ParsedLogEntry MakeLog(int line, Entity source, Entity target, ulong effectId, ulong abilityId,
            double raw, double effective, double mitigated, double threat) => new()
        {
            LogLineNumber = line,
            TimeStamp = start.AddSeconds(line),
            SourceInfo = new EntityInfo { Entity = source, MaxHP = 10000 },
            TargetInfo = new EntityInfo { Entity = target, MaxHP = 10000 },
            Ability = "ability",
            AbilityId = abilityId,
            Effect = new Effect { EffectType = EffectType.Apply, EffectId = effectId },
            Value = new Value { DblValue = raw, EffectiveDblValue = effective, MitigatedDblValue = mitigated },
            Threat = threat
        };
        var first = MakeLog(1, player, enemy, _7_0LogParsing._damageEffectId, 1, 100, 80, 70, 100);
        var second = MakeLog(2, player, enemy, _7_0LogParsing._damageEffectId, 1, 50, 40, 30, -60);
        var excludedHeal = MakeLog(3, player, player, _7_0LogParsing._healEffectId, 813707324030976, 200, 180, 0, 0);
        var excludedMaximum = MakeLog(4, player, player, _7_0LogParsing._healEffectId, 864164599824384, 300, 250, 0, 0);
        var smallHit1 = MakeLog(5, player, enemy, _7_0LogParsing._damageEffectId, 1, 200, 200, 200, 0);
        var smallHit2 = MakeLog(6, player, enemy, _7_0LogParsing._damageEffectId, 1, 200, 200, 200, 0);
        var smallHit3 = MakeLog(7, player, enemy, _7_0LogParsing._damageEffectId, 1, 200, 200, 200, 0);
        smallHit2.TimeStamp = smallHit1.TimeStamp.AddMilliseconds(300);
        smallHit3.TimeStamp = smallHit1.TimeStamp.AddMilliseconds(600);
        var logs = new[] { first, second, excludedHeal, excludedMaximum, smallHit1, smallHit2, smallHit3 };

        var snapshot = CombatIdentifier.GenerateCombatSnapshotFromLogs(logs);
        var live = new Combat();
        CombatIdentifier.GenerateCombatFromLogs(new[] { first }, combatShell: live);
        CombatIdentifier.GenerateCombatFromLogs(new[] { second, excludedHeal, excludedMaximum, smallHit1 }, combatShell: live);
        CombatIdentifier.GenerateCombatFromLogs(new[] { smallHit2, smallHit3 }, combatShell: live);

        Assert.That(snapshot.TotalThreat[player], Is.EqualTo(40));
        Assert.That(live.TotalThreat[player], Is.EqualTo(snapshot.TotalThreat[player]));
        Assert.That(snapshot.TotalEffectiveDamageTaken[enemy], Is.EqualTo(700));
        Assert.That(live.TotalEffectiveDamageTaken[enemy], Is.EqualTo(snapshot.TotalEffectiveDamageTaken[enemy]));
        Assert.That(snapshot.TotalEffectiveHealing[player], Is.EqualTo(250));
        Assert.That(live.TotalEffectiveHealing[player], Is.EqualTo(snapshot.TotalEffectiveHealing[player]));
        Assert.That(live.MaxHeal[player], Is.EqualTo(snapshot.MaxHeal[player]));
        Assert.That(live.MaxEffectiveHeal[player], Is.EqualTo(snapshot.MaxEffectiveHeal[player]));
        Assert.That(snapshot.BigDamageTimestamps[enemy], Is.EqualTo(new[] { smallHit3.TimeStamp }));
        Assert.That(live.BigDamageTimestamps[enemy], Is.EqualTo(snapshot.BigDamageTimestamps[enemy]));
    }
}
