using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.LogIndex;

/// <summary>Small, persistent metadata only. Never contains parsed combat events.</summary>
public sealed class IndexedCombatLog
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public DateTime LastWriteTimeUtc { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public List<string> Players { get; set; } = new();
    public List<IndexedEncounter> Encounters { get; set; } = new();
}

public sealed class IndexedEncounter
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string ActivityType { get; set; } = "Other";
    public string EncounterName { get; set; } = "Open World";
    public string BossName { get; set; } = "";
    public string Difficulty { get; set; } = "";
    /// <summary>Declared group size; zero means the log did not declare a size.</summary>
    public int PlayerCount { get; set; }
    public List<string> Players { get; set; } = new();
    public string Outcome { get; set; } = "Combat";
}

public sealed class CombatLogSearchFilter
{
    public string? ActivityType { get; set; }
    public string? EncounterName { get; set; }
    public string? BossName { get; set; }
    public string? Difficulty { get; set; }
    public string? PlayerName { get; set; }
    public int? PlayerCount { get; set; }
    public bool? IsPvp { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public static class CombatLogSearch
{
    public static IReadOnlyList<IndexedCombatLog> Search(IEnumerable<IndexedCombatLog> logs, CombatLogSearchFilter filter)
    {
        bool hasEncounterFilter = !string.IsNullOrWhiteSpace(filter.ActivityType) ||
            !string.IsNullOrWhiteSpace(filter.EncounterName) || !string.IsNullOrWhiteSpace(filter.BossName) ||
            !string.IsNullOrWhiteSpace(filter.Difficulty) || filter.PlayerCount.HasValue || filter.IsPvp.HasValue;
        return logs.Where(log =>
                (!filter.FromDate.HasValue || log.EndTime.Date >= filter.FromDate.Value.Date) &&
                (!filter.ToDate.HasValue || log.StartTime.Date <= filter.ToDate.Value.Date) &&
                (hasEncounterFilter
                    ? log.Encounters.Any(e => MatchesEncounter(e, filter))
                    : MatchesPlayer(log.Players, filter.PlayerName)))
            .OrderByDescending(log => log.StartTime).ThenBy(log => log.FileName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool MatchesEncounter(IndexedEncounter encounter, CombatLogSearchFilter filter) =>
        Matches(encounter.ActivityType, filter.ActivityType) && Matches(encounter.EncounterName, filter.EncounterName) &&
        Matches(encounter.BossName, filter.BossName) && Matches(encounter.Difficulty, filter.Difficulty) &&
        (!filter.PlayerCount.HasValue || encounter.PlayerCount == filter.PlayerCount.Value) &&
        (!filter.IsPvp.HasValue || (encounter.ActivityType == "PvP") == filter.IsPvp.Value) &&
        (!filter.FromDate.HasValue || encounter.EndTime.Date >= filter.FromDate.Value.Date) &&
        (!filter.ToDate.HasValue || encounter.StartTime.Date <= filter.ToDate.Value.Date) &&
        MatchesPlayer(encounter.Players, filter.PlayerName);

    private static bool Matches(string value, string? filter) =>
        string.IsNullOrWhiteSpace(filter) || value.Equals(filter, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesPlayer(IEnumerable<string> players, string? name) =>
        string.IsNullOrWhiteSpace(name) || players.Any(p => p.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase));
}

public sealed record LogIndexProgress(int ProcessedFiles, int TotalFiles, int IndexedFiles, int ReusedFiles, int FailedFiles,
    string Message, IReadOnlyList<IndexedCombatLog>? AvailableLogs = null);
