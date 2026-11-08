using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Orbs.RunRace;

namespace SWTORCombatParser.Model.RunRace;

/// <summary>Small local run history. Retains a personal best plus recent runs for each exact cohort.</summary>
public sealed class RunGhostStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private List<RunGhost>? _runs;

    public RunGhostStore(string? path = null) => _path = path ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser", "run_ghosts.json");

    public IReadOnlyList<RunGhost> Read()
    {
        lock (_gate)
        {
            if (_runs != null) return _runs.ToArray();
            try
            {
                _runs = (JsonSerializer.Deserialize<List<RunGhost>>(File.ReadAllText(_path)) ?? new())
                    .Where(IsValid).Select(run => run with
                    {
                        Cohort = run.Cohort with { GroupSize = RunRouteIdentity.GroupSize(run.Cohort.GroupSize) },
                        IsComplete = run.IsComplete && (RunRouteCatalog.Find(run.Cohort.Encounter)?.IsComplete(run.Splits.OrderBy(s => s.EndSeconds).Select(s => s.Boss)) ?? true)
                    }).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
            {
                _runs = new List<RunGhost>();
            }
            return _runs.ToArray();
        }
    }

    public void Save(RunGhost run)
    {
        if (!IsValid(run)) return;
        lock (_gate)
        {
            Read();
            run = run with { Cohort = run.Cohort with { GroupSize = RunRouteIdentity.GroupSize(run.Cohort.GroupSize) } };
            var existing = _runs!.FirstOrDefault(saved => saved.RunId == run.RunId);
            if (existing != null && (existing.IsComplete && !run.IsComplete || existing.Splits.Count > run.Splits.Count ||
                existing.DurationSeconds > run.DurationSeconds)) return;
            if (existing == run) return;
            var updated = _runs.Where(saved => saved.RunId != run.RunId).Append(run).ToArray();
            var retained = updated.Where(r => r.IsComplete).GroupBy(item => (item.Cohort, Player: item.Player.ToUpperInvariant(), item.RouteKey))
                .SelectMany(group => group.OrderBy(item => item.DurationSeconds).Take(1)
                    .Concat(group.OrderByDescending(item => item.Completed).Take(5)).Distinct())
                .Concat(updated.Where(r => !r.IsComplete).GroupBy(r => (r.Cohort, Player: r.Player.ToUpperInvariant()))
                    .SelectMany(group => group.OrderByDescending(r => r.Entered).Take(5))).ToList();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, retained);
                    stream.Flush(true);
                }
                File.Move(temporary, _path, true);
                _runs = retained;
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    public static bool IsValid(RunGhost? run) => run?.Cohort != null && run.Player != null && run.Entered != default && run.Completed >= run.Entered &&
        !string.IsNullOrWhiteSpace(run.Cohort.Encounter) && !string.IsNullOrWhiteSpace(run.Cohort.Difficulty) &&
        !string.IsNullOrWhiteSpace(run.Cohort.GroupSize) && run.Splits != null && run.Splits.Count > 0 &&
        run.Splits.All(split => split != null && !string.IsNullOrWhiteSpace(split.Boss) && double.IsFinite(split.StartSeconds) &&
            double.IsFinite(split.EndSeconds) && split.StartSeconds >= 0 && split.EndSeconds >= split.StartSeconds &&
            split.EndSeconds <= (run.Completed - run.Entered).TotalSeconds) &&
        run.Splits.Select(split => split.Boss).Distinct().Count() == run.Splits.Count;
}
