using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;

namespace SWTORCombatParser_Test;

public class NetworkAndLeaderboardTests
{
    [Test]
    public async Task UploadPreparationFailureReturnsAnError()
    {
        var result = await ParselyUploader.TryUploadText(null, "combat.txt");
        Assert.That(result.WasSuccess, Is.False);
        Assert.That(result.ErrorMessage, Is.Not.Empty);
    }

    [Test]
    public void LeaderboardMetricsHandleAbsentValuesAndUseParticipantTotals()
    {
        var player = new Entity();
        var combat = new Combat { DurationOverride = 10000 };
        combat.TotalEffectiveFluffDamage[player] = 150;
        combat.TotalEffectiveFocusDamage[player] = 50;
        combat.TotalHealing[player] = 70;
        combat.TotalProvidedSheilding[player] = 30;
        var method = typeof(Leaderboards).GetMethod("GetValueForLeaderboardEntry", BindingFlags.NonPublic | BindingFlags.Static);

        double Get(LeaderboardEntryType type) => (double)method.Invoke(null, new object[] { type, combat, player });
        Assert.That(Get(LeaderboardEntryType.Damage), Is.EqualTo(20));
        Assert.That(Get(LeaderboardEntryType.FocusDPS), Is.EqualTo(5));
        Assert.That(Get(LeaderboardEntryType.Healing), Is.EqualTo(10));
        Assert.That(Get(LeaderboardEntryType.EffectiveHealing), Is.EqualTo(3));
        Assert.That(Get(LeaderboardEntryType.Mitigation), Is.Zero);
        combat.DurationOverride = 0;
        Assert.That(Get(LeaderboardEntryType.Damage), Is.Zero);
    }
}
