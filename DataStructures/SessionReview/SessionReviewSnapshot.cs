using System;
using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures.ClassInfos;

namespace SWTORCombatParser.DataStructures.SessionReview;

public enum SessionCombatOutcome { Kill, Wipe, Combat, PvP, InProgress }

/// <summary>Small, detached summaries; no parser state or full combat logs are retained.</summary>
public sealed record SessionPlayerMetrics(double Damage = 0, double EffectiveHealing = 0,
    double DamageTaken = 0, int Deaths = 0)
{
    public static SessionPlayerMetrics operator +(SessionPlayerMetrics a, SessionPlayerMetrics b) =>
        new(a.Damage + b.Damage, a.EffectiveHealing + b.EffectiveHealing,
            a.DamageTaken + b.DamageTaken, a.Deaths + b.Deaths);
}

public sealed record SessionCombatSummary(string Name, DateTime Start, DateTime End,
    SessionCombatOutcome Outcome, IReadOnlyDictionary<string, SessionPlayerMetrics> Players,
    IReadOnlyList<string> LocalPlayers, bool HasData = true)
{
    public bool IsBoss { get; init; } = Outcome is SessionCombatOutcome.Kill or SessionCombatOutcome.Wipe;
    public IReadOnlyDictionary<string, SessionPlayerInfo> PlayerInfo { get; init; } = new Dictionary<string, SessionPlayerInfo>();
    public IReadOnlyDictionary<string, IReadOnlyList<SessionMetricSample>> Samples { get; init; } = new Dictionary<string, IReadOnlyList<SessionMetricSample>>();
    public double DurationSeconds => Math.Max(0, (End - Start).TotalSeconds);
    public bool HasPlayer(string? name) => name is null ? LocalPlayers.Any(Players.ContainsKey) : Players.ContainsKey(name);
    public SessionPlayerMetrics ForPlayer(string? name) => name is null
        ? LocalPlayers.Where(Players.ContainsKey).Aggregate(new SessionPlayerMetrics(), (total, p) => total + Players[p])
        : Players.GetValueOrDefault(name) ?? new SessionPlayerMetrics();
}

public sealed record SessionPlayerInfo(Role Role = Role.Unknown, string Discipline = "");

/// <summary>One second of detached totals and the last observed HP in that second. Null HP is unknown, not zero.</summary>
public sealed record SessionMetricSample(DateTime Time, SessionPlayerMetrics Metrics, double? Health, double? HealthPercent);

public sealed record SessionReviewSnapshot(string Name, string Details, IReadOnlyList<SessionCombatSummary> Combats)
{
    public DateTime? RangeStart { get; init; }
    public DateTime? RangeEnd { get; init; }
    public DateTime Start => RangeStart ?? (Combats.Count == 0 ? DateTime.MinValue : Combats.Min(c => c.Start));
    public DateTime End => RangeEnd ?? (Combats.Count == 0 ? DateTime.MinValue : Combats.Max(c => c.End));
    public double ElapsedSeconds => Math.Max(0, (End - Start).TotalSeconds);
    // Union intervals: overlapping or duplicate combat ranges must not create negative downtime.
    public double CombatSeconds => UnionSeconds(Combats);
    public double PlayerCombatSeconds(string? player) => UnionSeconds(Combats.Where(c => c.HasData && c.HasPlayer(player)));
    private static double UnionSeconds(IEnumerable<SessionCombatSummary> combats)
    {
            double seconds = 0;
            var end = DateTime.MinValue;
            foreach (var combat in combats.OrderBy(c => c.Start))
            {
                var from = combat.Start > end ? combat.Start : end;
                if (combat.End > from) seconds += (combat.End - from).TotalSeconds;
                if (combat.End > end) end = combat.End;
            }
            return seconds;
    }
    public double DowntimeSeconds => Math.Max(0, ElapsedSeconds - CombatSeconds);
    public int Kills => Combats.Count(c => c.Outcome == SessionCombatOutcome.Kill);
    public int Wipes => Combats.Count(c => c.Outcome == SessionCombatOutcome.Wipe);
    public int MissingCombats => Combats.Count(c => !c.HasData);
    public SessionPlayerMetrics ForPlayer(string? name) => Combats.Aggregate(new SessionPlayerMetrics(), (total, c) => total + c.ForPlayer(name));
    public double CombatDps(string? name) => PlayerCombatSeconds(name) > 0 ? ForPlayer(name).Damage / PlayerCombatSeconds(name) : 0;
    public double CombatEhps(string? name) => PlayerCombatSeconds(name) > 0 ? ForPlayer(name).EffectiveHealing / PlayerCombatSeconds(name) : 0;
    public double SessionDps(string? name) => ElapsedSeconds > 0 ? ForPlayer(name).Damage / ElapsedSeconds : 0;
    public SessionReviewSnapshot FilterBosses(bool bossesOnly) => !bossesOnly ? this : this with
    {
        Combats = Combats.Where(c => c.IsBoss).ToArray(), RangeStart = Start, RangeEnd = End
    };
}
