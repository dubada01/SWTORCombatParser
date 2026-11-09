using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace SWTORCombatParser.Model.Progression;

public enum ProgressionDataState { Pending, Available, Unavailable }
public enum ProgressionEventKind { Damage, Heal }

public sealed record ProgressionCohort(string Operation, string Difficulty, string GroupSize)
{
    [JsonIgnore] public string Label => $"{Operation}  ·  {Difficulty}  ·  {GroupSize}";
}

public sealed record ProgressionPullMetadata(string Id, string RaidId, ProgressionCohort Cohort, string Boss,
    DateTime Start, DateTime End, bool WasKilled)
{
    [JsonIgnore] public double DurationSeconds => Math.Max(0, (End - Start).TotalSeconds);
}

public sealed record ProgressionRecapEvent(DateTime Time, ProgressionEventKind Kind, ulong AbilityId,
    string? Ability, string? Source, double Amount);

public sealed record ProgressionDeath(string Player, DateTime Time, string? Phase, ProgressionRecapEvent? LastHit,
    bool IsExplicitReset, IReadOnlyList<ProgressionRecapEvent> Leadup);

public sealed record ProgressionPullSummary(ProgressionPullMetadata Metadata, ProgressionDataState State,
    string? LastPhase, string? EndingPhase, string PhaseRevision, IReadOnlyList<ProgressionDeath> Deaths)
{
    [JsonIgnore] public ProgressionDeath? FirstDeath => Deaths.FirstOrDefault();

    internal static ProgressionPullSummary Placeholder(ProgressionPullMetadata metadata, ProgressionDataState state) =>
        new(metadata, state, null, null, "", Array.Empty<ProgressionDeath>());

    internal ProgressionPullSummary WithoutPhases() => this with
    {
        LastPhase = null,
        EndingPhase = null,
        Deaths = Deaths.Select(death => death with { Phase = null }).ToArray()
    };
}

public sealed record ProgressionPattern(string Name, int PullCount);

internal static class ProgressionPatterns
{
    internal static ProgressionPattern[] EarlyHits(IEnumerable<ProgressionPullSummary> pulls) => pulls
        .Where(pull => !pull.Metadata.WasKilled && pull.State == ProgressionDataState.Available)
        .SelectMany(pull => pull.Deaths.Take(3).Where(death => death.LastHit?.Ability != null)
            .Select(death => death.LastHit!)
            .DistinctBy(AbilityKey)
            .Select(hit => (Key: AbilityKey(hit), Name: hit.Ability!)))
        .GroupBy(hit => hit.Key, StringComparer.OrdinalIgnoreCase)
        .Select(group => new ProgressionPattern(group.First().Name, group.Count()))
        .Where(pattern => pattern.PullCount > 1)
        .OrderByDescending(pattern => pattern.PullCount).ThenBy(pattern => pattern.Name).ToArray();

    internal static ProgressionPattern[] EndingPhases(IEnumerable<ProgressionPullSummary> pulls) => pulls
        .Where(pull => !pull.Metadata.WasKilled && pull.EndingPhase != null)
        .GroupBy(pull => pull.EndingPhase!, StringComparer.OrdinalIgnoreCase)
        .Select(group => new ProgressionPattern(group.Key, group.Count()))
        .Where(pattern => pattern.PullCount > 1)
        .OrderByDescending(pattern => pattern.PullCount).ThenBy(pattern => pattern.Name).ToArray();

    private static string AbilityKey(ProgressionRecapEvent hit) => hit.AbilityId == 0
        ? hit.Ability! : hit.AbilityId.ToString(CultureInfo.InvariantCulture);
}
