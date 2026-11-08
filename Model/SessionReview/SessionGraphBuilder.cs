using System;
using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures.SessionReview;

namespace SWTORCombatParser.Model.SessionReview;

public enum SessionGraphMetric { Dps, DirectEhps, Dtps, Health, HealthPercent }
public sealed record SessionGraphPoint(double StartSeconds, double EndSeconds, double? Value);
public sealed record SessionGraphSeries(string Player, IReadOnlyList<SessionGraphPoint> Points)
{
    public int ColorIndex { get; init; }
}
public sealed record SessionGraphData(SessionReviewSnapshot Snapshot, SessionGraphMetric Metric,
    int BucketSeconds, IReadOnlyList<SessionGraphSeries> Series);

/// <summary>Reuses detached samples; changing players, metric or scope never rereads a combat log.</summary>
public static class SessionGraphBuilder
{
    public static SessionGraphData Build(SessionReviewSnapshot snapshot, IEnumerable<string> players,
        SessionGraphMetric metric, int bucketSeconds = 5)
    {
        var health = metric is SessionGraphMetric.Health or SessionGraphMetric.HealthPercent;
        var bucket = health ? 1 : Math.Clamp(bucketSeconds, 1, 60);
        var series = new List<SessionGraphSeries>();
        foreach (var player in players.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var points = new List<SessionGraphPoint>();
            foreach (var combat in snapshot.Combats.OrderBy(c => c.Start))
            {
                if (!combat.HasData || !combat.HasPlayer(player) || combat.DurationSeconds <= 0) continue;
                var count = (int)Math.Ceiling(combat.DurationSeconds / bucket);
                var values = new double[count];
                var observed = new bool[count];
                if (combat.Samples.TryGetValue(player, out var samples))
                {
                    foreach (var sample in samples)
                    {
                        var seconds = (sample.Time - combat.Start).TotalSeconds;
                        if (seconds < 0 || seconds > combat.DurationSeconds) continue;
                        var index = Math.Min(count - 1, (int)(seconds / bucket));
                        if (health)
                        {
                            var hp = metric == SessionGraphMetric.Health ? sample.Health : sample.HealthPercent;
                            if (!hp.HasValue) continue;
                            values[index] = hp.Value;
                            observed[index] = true;
                        }
                        else values[index] += metric switch
                        {
                            SessionGraphMetric.DirectEhps => sample.Metrics.EffectiveHealing,
                            SessionGraphMetric.Dtps => sample.Metrics.DamageTaken,
                            _ => sample.Metrics.Damage
                        };
                    }
                }
                var offset = (combat.Start - snapshot.Start).TotalSeconds;
                // Explicit break even when combats touch: never connect different encounters or unknown HP.
                points.Add(new SessionGraphPoint(offset, offset, null));
                for (var i = 0; i < count; i++)
                {
                    var from = i * bucket;
                    var to = Math.Min(combat.DurationSeconds, from + bucket);
                    var value = health ? observed[i] ? (double?)values[i] : null : values[i] / (to - from);
                    points.Add(new SessionGraphPoint(offset + from, offset + to, value));
                }
            }
            series.Add(new SessionGraphSeries(player, points));
        }
        return new SessionGraphData(snapshot, metric, bucket, series);
    }
}
