using System;
using System.Collections.Generic;
using System.Linq;
using Orbs.RunRace;

namespace SWTORCombatParser.Model.RunRace;

public sealed record RunCohort(string Encounter, string Difficulty, string GroupSize);
public sealed record RunSplit(string Boss, double StartSeconds, double EndSeconds);
public sealed record RunGhost(RunCohort Cohort, DateTime Entered, DateTime Completed, string Player, IReadOnlyList<RunSplit> Splits, bool IsCustomRoute = false)
{
    public bool IsComplete { get; init; } = true;
    public string RunId => RunRouteIdentity.Visit(Player, Cohort.Encounter, Cohort.Difficulty, Cohort.GroupSize, Entered);
    public double DurationSeconds => Splits.Count == 0 ? 0 : Splits.Max(split => split.EndSeconds);
    public string RouteKey => RunRouteIdentity.Key(Splits.OrderBy(split => split.EndSeconds).Select(split => split.Boss));
}
public sealed record RaceRouteOption(string Key, string Label);
public sealed record RaceCheckpoint(string Boss, int Attempts, double? Seconds, double? GhostSeconds,
    double? DeltaSeconds, bool RouteMatches, double? StartSeconds = null, double? GhostStartSeconds = null,
    int? ReferenceSamples = null);
public sealed record RunRaceSnapshot(RunCohort? Cohort, DateTime Entered, double ElapsedSeconds,
    IReadOnlyList<RaceCheckpoint> Checkpoints, int Wipes, bool IsComplete, RunGhost? Ghost,
    string GhostMode, string Status, string RouteKey = "auto", IReadOnlyList<RaceRouteOption>? Routes = null, bool IsHistorical = false,
    string ReferenceLabel = "", double? ReferenceDurationSeconds = null, bool IsLeaderboardReference = false,
    bool ReferenceLoading = false, string? ActiveBossName = null, double? ActiveBossStartSeconds = null, bool IsPaused = false,
    bool IsReplay = false)
{
    public int Kills => Checkpoints.Count(checkpoint => checkpoint.Seconds.HasValue);
    public static RunRaceSnapshot Empty { get; } = new(null, default, 0, Array.Empty<RaceCheckpoint>(), 0, false, null,
        "Personal best", "Enter a Story operation or flashpoint to start a run.");
}

/// <summary>Area-entry timing. The encounter's final boss completes the observed route, including optional skips.</summary>
public sealed class RunRaceTracker
{
    private readonly List<RunSplit> _splits = new();
    private readonly Dictionary<string, int> _attempts = new(StringComparer.Ordinal);
    private readonly HashSet<(string Boss, DateTime Started)> _seen = new();
    private string[] _expected = Array.Empty<string>();
    private RunRouteDefinition? _route;
    public RunCohort? Cohort { get; private set; }
    public DateTime Entered { get; private set; }
    public DateTime Latest { get; private set; }
    public int Wipes { get; private set; }
    public bool IsComplete => _route?.IsComplete(_splits.Select(s => s.Boss)) == true;
    public IReadOnlyList<string> ExpectedBosses => _expected;

    public void Begin(RunCohort cohort, IEnumerable<string> bosses, DateTime entered, bool requireFirstBoss = true, string? completionBoss = null)
    {
        Cohort = cohort;
        Entered = Latest = entered;
        _expected = bosses.Where(boss => !string.IsNullOrWhiteSpace(boss)).Distinct(StringComparer.Ordinal).ToArray();
        _splits.Clear();
        _attempts.Clear();
        _seen.Clear();
        Wipes = 0;
        _route = _expected.Length == 0 ? null : new RunRouteDefinition(cohort.Encounter, requireFirstBoss, completionBoss ?? _expected[^1], _expected);
    }

    public bool AddAttempt(string boss, DateTime start, DateTime end, bool killed)
    {
        if (Cohort == null || Entered == default || start < Entered || end < start || IsComplete ||
            !_expected.Contains(boss, StringComparer.Ordinal) || !_seen.Add((boss, start))) return false;
        _attempts[boss] = _attempts.GetValueOrDefault(boss) + 1;
        if (killed && _splits.All(split => split.Boss != boss))
            _splits.Add(new RunSplit(boss, (start - Entered).TotalSeconds, (end - Entered).TotalSeconds));
        if (!killed) Wipes++;
        Advance(end);
        return true;
    }

    public void Advance(DateTime time)
    {
        if (time > Latest && !IsComplete) Latest = time;
        if (IsComplete) Latest = Entered.AddSeconds(_splits.Max(split => split.EndSeconds));
    }

    public RunGhost? CompletedRun(string player) => IsComplete && Cohort != null
        ? SavedRun(player) : null;

    public RunGhost? SavedRun(string player) => Cohort != null && _splits.Count > 0
        ? new RunGhost(Cohort, Entered, Entered.AddSeconds(_splits.Max(s => s.EndSeconds)), player,
            _splits.OrderBy(s => s.EndSeconds).ToArray(), _splits.Count != _expected.Length) { IsComplete = IsComplete } : null;

    public bool Accepts(RunGhost ghost) => ghost.IsComplete && Cohort == ghost.Cohort && Math.Abs((ghost.Entered - Entered).TotalSeconds) >= 1 &&
        _expected.Length > 0 &&
        ghost.Splits.All(split => _expected.Contains(split.Boss, StringComparer.Ordinal));

    public bool Follows(RunGhost ghost) => ghost.Splits.OrderBy(s => s.EndSeconds).Select(s => s.Boss)
        .Take(_splits.Count).SequenceEqual(_splits.OrderBy(s => s.EndSeconds).Select(s => s.Boss));

    public RunRaceSnapshot Snapshot(RunGhost? ghost, string mode, string status = "")
    {
        var ordered = _splits.OrderBy(split => split.EndSeconds).ToArray();
        var ghostSplits = ghost?.Splits.OrderBy(split => split.EndSeconds).ToArray() ?? Array.Empty<RunSplit>();
        var checkpoints = _expected.Select(boss =>
        {
            var own = ordered.FirstOrDefault(split => split.Boss == boss);
            var reference = ghostSplits.FirstOrDefault(split => split.Boss == boss);
            // Compare the same preceding sequence, never mix faster splits from different routes.
            var sameRoute = own == null || reference == null || ordered.TakeWhile(split => split.Boss != boss).Select(split => split.Boss)
                .SequenceEqual(ghostSplits.TakeWhile(split => split.Boss != boss).Select(split => split.Boss));
            return new RaceCheckpoint(boss, _attempts.GetValueOrDefault(boss), own?.EndSeconds, reference?.EndSeconds,
                own != null && reference != null && sameRoute ? own.EndSeconds - reference.EndSeconds : null, sameRoute,
                own?.StartSeconds, reference?.StartSeconds);
        }).ToArray();
        if (status.Length == 0) status = IsComplete ? "Run complete · route saved automatically"
            : ghost == null ? "No matching completed run yet. Boss checkpoints save automatically."
            : "Racing from area entry · splits compare the same completed checkpoints";
        return new RunRaceSnapshot(Cohort, Entered, Math.Max(0, (Latest - Entered).TotalSeconds), checkpoints,
            Wipes, IsComplete, ghost, mode, status);
    }
}
