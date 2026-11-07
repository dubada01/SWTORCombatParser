using NUnit.Framework;
using SWTORCombatParser.ViewModels.Overlays;

namespace SWTORCombatParser_Test;

public class OverlayLeaderboardRankTests
{
    [Test]
    public void MissingRankClearsPreviousRankAndAllowsNextUpdate()
    {
        // Exercise rank updates without initializing Avalonia bitmap resources.
        var metric = (OverlayMetricInfo)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(OverlayMetricInfo));
        metric.LeaderboardRank = "95";
        Assert.That(metric.LeaderboardRank, Is.EqualTo("95"));
        metric.LeaderboardRank = "—";
        Assert.That(metric.LeaderboardRank, Is.Empty);
        metric.LeaderboardRank = "82";
        Assert.That(metric.LeaderboardRank, Is.EqualTo("82"));
        metric.LeaderboardRank = "0";
        Assert.That(metric.LeaderboardRank, Is.EqualTo("0"));
        metric.LeaderboardRank = "";
        Assert.That(metric.LeaderboardRank, Is.Empty);
    }
}
