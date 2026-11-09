using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Progression;

public sealed record ProgressionRaidChoice(string Id, string Operation, DateTime Start, bool IsReplay)
{
    public string Label => $"{Operation}  ·  {Start:g}" + (IsReplay ? "  ·  Replay" : "");
}

internal sealed record ProgressionSavedRaid(ProgressionRaidChoice Raid, string? Team, IReadOnlyList<ProgressionPullSummary> Pulls);
internal sealed record ProgressionHistorySnapshot(long Revision, IReadOnlyList<ProgressionSavedRaid> Raids, bool HasReadErrors);

/// <summary>One atomically replaced document per raid visit, with detached pull summaries.</summary>
internal sealed class ProgressionHistoryStore
{
    private const int Version = 1;
    private static readonly object WriteGate = new();
    private readonly object _gate = new();
    private readonly string _directory;
    private Dictionary<string, ProgressionSavedRaid>? _raids;
    private bool _hasReadErrors;
    private long _revision;

    internal ProgressionHistoryStore(string? directory = null) => _directory = directory ?? Path.Combine(
        LogLoadDiagnostics.ProfileDataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DubaTech", "SWTORCombatParser"), "progression_history");

    internal ProgressionHistorySnapshot Read()
    {
        lock (_gate)
        {
            if (_raids == null)
            {
                _raids = new Dictionary<string, ProgressionSavedRaid>(StringComparer.Ordinal);
                try
                {
                    if (Directory.Exists(_directory))
                        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
                        {
                            try
                            {
                                var saved = ReadRaid(path);
                                _raids.Add(saved.Raid.Id, saved);
                            }
                            catch (Exception error) when (IsFileError(error)) { _hasReadErrors = true; }
                        }
                }
                catch (Exception error) when (IsFileError(error)) { _hasReadErrors = true; }
            }
            return new ProgressionHistorySnapshot(_revision, _raids.Values.ToArray(), _hasReadErrors);
        }
    }

    internal void Upsert(ProgressionRaidChoice raid, IEnumerable<ProgressionPullSummary> pulls) => Update(raid, current =>
    {
        var indexed = current.Pulls.ToDictionary(pull => pull.Metadata.Id, StringComparer.Ordinal);
        var changed = false;
        foreach (var pull in pulls)
        {
            if (pull.State == ProgressionDataState.Pending ||
                indexed.TryGetValue(pull.Metadata.Id, out var old) &&
                (old == pull || old.State == ProgressionDataState.Available && pull.State != ProgressionDataState.Available))
                continue;
            indexed[pull.Metadata.Id] = pull;
            changed = true;
        }
        return changed ? current with { Pulls = indexed.Values.OrderBy(pull => pull.Metadata.Start).ToArray() } : current;
    });

    internal void AssignTeam(ProgressionRaidChoice raid, string? team)
    {
        var trimmed = team?.Trim();
        var name = string.IsNullOrEmpty(trimmed) ? null : trimmed;
        Update(raid, current => current with { Team = name });
    }

    private void Update(ProgressionRaidChoice raid, Func<ProgressionSavedRaid, ProgressionSavedRaid> edit)
    {
        if (raid.IsReplay) return;
        if (!IsRaidId(raid.Id)) throw new InvalidDataException("Invalid progression raid identity.");
        lock (_gate)
        lock (WriteGate)
        {
            Read();
            var path = Path.Combine(_directory, raid.Id + ".json");
            // Read the newest document before merging so a stale instance can't undo a team assignment.
            var current = File.Exists(path) ? ReadRaid(path) : new ProgressionSavedRaid(raid, null, Array.Empty<ProgressionPullSummary>());
            var updated = edit(current);
            if (current == updated && File.Exists(path)) return;
            Directory.CreateDirectory(_directory);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, new RaidDocument(Version, updated));
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, path, overwrite: true);
                _raids![raid.Id] = updated;
                _revision++;
            }
            finally
            {
                try { File.Delete(temporary); }
                catch (Exception error) when (IsFileError(error)) { }
            }
        }
    }

    private static ProgressionSavedRaid ReadRaid(string path)
    {
        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize<RaidDocument>(stream);
        if (document?.Version != Version || document.Saved is not { Raid: { } raid, Pulls: { } pulls } saved ||
            !IsRaidId(raid.Id) || raid.Id != Path.GetFileNameWithoutExtension(path) || raid.IsReplay ||
            string.IsNullOrWhiteSpace(raid.Operation) || raid.Start == default || pulls.Any(pull => !IsValid(pull, raid.Id)) ||
            pulls.Select(pull => pull.Metadata.Id).Distinct(StringComparer.Ordinal).Count() != pulls.Count)
            throw new InvalidDataException("Invalid progression history document.");
        return saved;
    }

    private static bool IsValid(ProgressionPullSummary? pull, string raidId) => pull is
        { Metadata: { Cohort: { } cohort } info, Deaths: { } deaths, PhaseRevision: not null,
            State: ProgressionDataState.Available or ProgressionDataState.Unavailable } &&
        !string.IsNullOrEmpty(info.Id) && info.RaidId == raidId && !string.IsNullOrWhiteSpace(info.Boss) &&
        !string.IsNullOrWhiteSpace(cohort.Operation) && !string.IsNullOrWhiteSpace(cohort.Difficulty) &&
        !string.IsNullOrWhiteSpace(cohort.GroupSize) && info.Start != default && info.End >= info.Start &&
        deaths.All(death => death is { Leadup: { } leadup } && !string.IsNullOrWhiteSpace(death.Player) &&
            death.Time >= info.Start && death.Time <= info.End && leadup.Count <= ProgressionSummaryBuilder.LeadupLimit &&
            leadup.All(item => item != null) && (death.LastHit == null || double.IsFinite(death.LastHit.Amount)));

    private static bool IsRaidId(string? id) => id?.Length == 64 && id.All(Uri.IsHexDigit);
    internal static bool IsFileError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException;

    private sealed record RaidDocument(int Version, ProgressionSavedRaid Saved);
}
