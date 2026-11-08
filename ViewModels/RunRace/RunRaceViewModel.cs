using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Model.RunRace;

namespace SWTORCombatParser.ViewModels.RunRace;

public sealed class RunRaceViewModel : ReactiveObject, IDisposable
{
    private static readonly IReadOnlyList<RaceRouteOption> DefaultRoutes = new[] { new RaceRouteOption("auto", "Automatic · follow your boss order") };
    private static readonly IBrush PaceAheadBrush = Brush.Parse("#8AE3EA");
    private static readonly IBrush PaceBehindBrush = Brush.Parse("#E5B482");
    private readonly RunRaceService _service;
    private RunRaceSnapshot _snapshot;
    private bool _disposed;
    private bool _updating;
    public event Action? DisplayUpdated;
    public RunRaceViewModel(RunRaceService service)
    {
        _service = service;
        _snapshot = service.Snapshot;
        _service.Updated += OnUpdated;
        Rows = BuildRows(_snapshot);
        RefreshReferenceCommand = ReactiveCommand.Create(_service.RefreshReference,
            this.WhenAnyValue(vm => vm.CanRefreshReference));
    }

    public IReadOnlyList<string> GhostModes { get; } = new[] { "Personal best", "Latest run", RunRaceService.LeaderboardMode };
    public ReactiveCommand<Unit, Unit> RefreshReferenceCommand { get; }
    public bool IsLeaderboardReference => _snapshot.IsLeaderboardReference;
    public bool CanChooseRoute => AvailableRoutes.Count > 1;
    public bool CanRefreshReference => !_snapshot.ReferenceLoading && HasRun;
    public IReadOnlyList<RaceRouteOption> AvailableRoutes => _snapshot.Routes ?? DefaultRoutes;
    public RaceRouteOption? SelectedRoute
    {
        get => AvailableRoutes.FirstOrDefault(route => route.Key == _snapshot.RouteKey);
        set { if (!_updating && value != null && value.Key != _snapshot.RouteKey) _service.SetRoute(value.Key); }
    }
    public string GhostMode { get => _snapshot.GhostMode; set { if (!_updating && value != null && value != _snapshot.GhostMode) _service.SetGhostMode(value); } }
    public string Title => _snapshot.Cohort?.Encounter ?? "Run Race";
    public string Details => _snapshot.Cohort == null ? "Story operations & flashpoints"
        : $"{_snapshot.Cohort.Difficulty} · {_snapshot.Cohort.GroupSize.Replace(" Players", "", StringComparison.OrdinalIgnoreCase).Replace(" Player", "", StringComparison.OrdinalIgnoreCase)} players · timed from area entry";
    public string Status => _snapshot.Status;
    public RunRaceSnapshot TimelineSnapshot => _snapshot;
    public bool HasRun => _snapshot.Cohort != null;
    public bool HasGhost => _snapshot.Checkpoints.Any(checkpoint => checkpoint.GhostSeconds > 0);
    public bool NeedsGhost => !HasGhost && !_snapshot.IsLeaderboardReference && !_snapshot.ReferenceLoading;
    public string Elapsed => FormatTime(_snapshot.ElapsedSeconds);
    public string ProgressLabel => $"{_snapshot.Kills} / {_snapshot.Checkpoints.Count} bosses · {_snapshot.Wipes} wipes";
    public string ReferenceTitle => _snapshot.ReferenceLabel.Length > 0 ? _snapshot.ReferenceLabel : _snapshot.GhostMode;
    public string GhostDescription => !HasRun ? "Reference loads when you enter a supported instance"
        : _snapshot.ReferenceLoading ? "Loading reference timings…"
        : _snapshot.IsLeaderboardReference ? HasGhost ? $"{ReferenceTitle} · {FormatTime(_snapshot.ReferenceDurationSeconds ?? _snapshot.Checkpoints.Max(checkpoint => checkpoint.GhostSeconds ?? 0))} latest checkpoint" : "No matching leaderboard timings available"
        : _snapshot.Ghost == null ? "No completed run found for this character and route"
        : $"{_snapshot.GhostMode} · {FormatTime(_snapshot.Ghost.DurationSeconds)} · {_snapshot.Ghost.Completed:MMM d, yyyy}";
    public string ReferenceHint => _snapshot.IsLeaderboardReference
        ? "Per-boss leaderboard timings · fastest 10% · not a single recorded run"
        : "One recorded run · same encounter, difficulty, group size & route";
    public string GhostPlayer => _snapshot.Ghost?.Player ?? "";
    public double LiveProgress => _snapshot.Checkpoints.Count == 0 ? 0 : _snapshot.Kills * 100d / _snapshot.Checkpoints.Count;
    public double GhostProgress => !HasGhost ? 0 : _snapshot.Checkpoints.Count(checkpoint => checkpoint.GhostSeconds > 0 && checkpoint.GhostSeconds <= _snapshot.ElapsedSeconds) * 100d /
        _snapshot.Checkpoints.Count(checkpoint => checkpoint.GhostSeconds > 0);
    private RaceCheckpoint? NextReferenceCheckpoint =>
        _snapshot.Checkpoints.FirstOrDefault(checkpoint => checkpoint.Boss == _snapshot.ActiveBossName && checkpoint.GhostSeconds > 0)
        ?? _snapshot.Checkpoints.Where(checkpoint => !checkpoint.Seconds.HasValue && checkpoint.GhostSeconds > 0)
            .OrderBy(checkpoint => checkpoint.GhostSeconds).FirstOrDefault();
    public string PaceHeadline
    {
        get
        {
            if (!HasRun) return "Enter a Story operation or flashpoint to start";
            if (_snapshot.IsComplete) return "Route finished · " + ProgressLabel;
            if (_snapshot.IsPaused) return "Monitoring stopped · resume live parsing to continue";
            var next = NextReferenceCheckpoint;
            if (next == null) return !string.IsNullOrEmpty(_snapshot.ActiveBossName) ? "In combat · " + _snapshot.ActiveBossName
                : HasGhost ? "All available reference checkpoints reached" : _snapshot.ReferenceLoading ? "Fetching the matching reference…" : "Your run is recording · select comparison in the main app";
            var remaining = next.GhostSeconds!.Value - _snapshot.ElapsedSeconds;
            return $"{next.Boss} · {FormatTime(Math.Abs(remaining))} {(remaining >= 0 ? "to reference kill" : "past reference kill")}";
        }
    }
    public IBrush PaceBrush => NextReferenceCheckpoint?.GhostSeconds < _snapshot.ElapsedSeconds ? PaceBehindBrush : PaceAheadBrush;
    public string DeltaHeadline
    {
        get
        {
            var latest = _snapshot.Checkpoints.Where(checkpoint => checkpoint.Seconds.HasValue && checkpoint.GhostSeconds.HasValue)
                .OrderByDescending(checkpoint => checkpoint.Seconds).FirstOrDefault();
            if (latest == null || !HasGhost) return HasGhost ? "Awaiting first checkpoint" : _snapshot.ReferenceLoading ? "Loading reference" : "No reference yet";
            if (!latest.RouteMatches) return "Different boss order · split not comparable";
            return latest.DeltaSeconds.HasValue ? FormatDelta(latest.DeltaSeconds.Value) : "No matching checkpoint";
        }
    }
    public IBrush DeltaBrush
    {
        get
        {
            var latest = _snapshot.Checkpoints.Where(checkpoint => checkpoint.DeltaSeconds.HasValue).OrderByDescending(checkpoint => checkpoint.Seconds).FirstOrDefault();
            return latest?.DeltaSeconds is < 0 ? Brushes.MediumSeaGreen : latest?.DeltaSeconds is > 0 ? Brushes.SandyBrown : Brushes.LightGray;
        }
    }
    public IReadOnlyList<RunRaceRowViewModel> Rows { get; private set; }

    private void OnUpdated(RunRaceSnapshot snapshot) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed) return;
        // Replacing ComboBox items can write its retained selection back through a two-way binding.
        // Snapshot notifications are display updates; only a subsequent user selection may change the service.
        _updating = true;
        try
        {
            var previous = _snapshot;
            var checkpointsChanged = !previous.Checkpoints.SequenceEqual(snapshot.Checkpoints);
            var routesChanged = !(previous.Routes ?? DefaultRoutes).SequenceEqual(snapshot.Routes ?? DefaultRoutes);
            var ghostChanged = previous.Ghost != snapshot.Ghost || previous.IsLeaderboardReference != snapshot.IsLeaderboardReference ||
                previous.ReferenceLabel != snapshot.ReferenceLabel || previous.ReferenceDurationSeconds != snapshot.ReferenceDurationSeconds ||
                previous.ReferenceLoading != snapshot.ReferenceLoading;
            var cohortChanged = previous.Cohort != snapshot.Cohort;
            _snapshot = snapshot;
            // This redraws a single lightweight control, without replacing checkpoint rows or dropdowns.
            this.RaisePropertyChanged(nameof(TimelineSnapshot));
            if (Math.Floor(previous.ElapsedSeconds) != Math.Floor(snapshot.ElapsedSeconds) || ghostChanged)
            {
                this.RaisePropertyChanged(nameof(Elapsed));
                this.RaisePropertyChanged(nameof(GhostProgress));
            }
            if (Math.Floor(previous.ElapsedSeconds) != Math.Floor(snapshot.ElapsedSeconds) || ghostChanged || checkpointsChanged ||
                previous.ActiveBossName != snapshot.ActiveBossName || previous.ActiveBossStartSeconds != snapshot.ActiveBossStartSeconds ||
                previous.IsComplete != snapshot.IsComplete || previous.IsPaused != snapshot.IsPaused)
            {
                this.RaisePropertyChanged(nameof(PaceHeadline));
                this.RaisePropertyChanged(nameof(PaceBrush));
            }
            if (checkpointsChanged || previous.ActiveBossName != snapshot.ActiveBossName)
            {
                Rows = BuildRows(snapshot);
                this.RaisePropertyChanged(nameof(Rows));
            }
            if (checkpointsChanged) this.RaisePropertyChanged(nameof(LiveProgress));
            if (checkpointsChanged || previous.Wipes != snapshot.Wipes)
                this.RaisePropertyChanged(nameof(ProgressLabel));
            if (checkpointsChanged || ghostChanged)
            {
                this.RaisePropertyChanged(nameof(DeltaHeadline));
                this.RaisePropertyChanged(nameof(DeltaBrush));
            }
            if (cohortChanged)
            {
                this.RaisePropertyChanged(nameof(Title));
                this.RaisePropertyChanged(nameof(Details));
                this.RaisePropertyChanged(nameof(HasRun));
                DisplayUpdated?.Invoke();
            }
            if (ghostChanged || checkpointsChanged)
            {
                this.RaisePropertyChanged(nameof(HasGhost));
                this.RaisePropertyChanged(nameof(NeedsGhost));
                this.RaisePropertyChanged(nameof(GhostPlayer));
            }
            if (ghostChanged || cohortChanged)
            {
                this.RaisePropertyChanged(nameof(IsLeaderboardReference));
                this.RaisePropertyChanged(nameof(CanChooseRoute));
                this.RaisePropertyChanged(nameof(CanRefreshReference));
            }
            if (ghostChanged || cohortChanged || previous.GhostMode != snapshot.GhostMode)
            {
                this.RaisePropertyChanged(nameof(GhostDescription));
                this.RaisePropertyChanged(nameof(GhostMode));
                this.RaisePropertyChanged(nameof(ReferenceTitle));
                this.RaisePropertyChanged(nameof(ReferenceHint));
            }
            if (routesChanged)
            {
                this.RaisePropertyChanged(nameof(AvailableRoutes));
                this.RaisePropertyChanged(nameof(CanChooseRoute));
            }
            if (routesChanged || previous.RouteKey != snapshot.RouteKey) this.RaisePropertyChanged(nameof(SelectedRoute));
            if (previous.Status != snapshot.Status) this.RaisePropertyChanged(nameof(Status));
        }
        finally { _updating = false; }
    });

    private static IReadOnlyList<RunRaceRowViewModel> BuildRows(RunRaceSnapshot snapshot) => snapshot.Checkpoints
        .Select((checkpoint, index) => new RunRaceRowViewModel(checkpoint, index + 1, checkpoint.Boss == snapshot.ActiveBossName)).ToArray();
    public static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds > TimeSpan.MaxValue.TotalSeconds - 1) return "—";
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }
    public static string FormatDelta(double delta) => Math.Abs(delta) < 0.5 ? "Even with reference"
        : $"{(delta < 0 ? "−" : "+")}{FormatTime(Math.Abs(delta))} {(delta < 0 ? "ahead" : "behind")}";
    public void Dispose() { if (_disposed) return; _disposed = true; _service.Updated -= OnUpdated; RefreshReferenceCommand.Dispose(); DisplayUpdated = null; }
}

public sealed class RunRaceRowViewModel
{
    public RunRaceRowViewModel(RaceCheckpoint checkpoint, int number, bool isActive = false)
    {
        Number = number.ToString("00");
        Boss = checkpoint.Boss;
        Attempts = isActive ? "In combat" : checkpoint.Attempts == 0 ? "Pending" : $"{checkpoint.Attempts} {(checkpoint.Attempts == 1 ? "attempt" : "attempts")}";
        LiveTime = checkpoint.Seconds.HasValue ? RunRaceViewModel.FormatTime(checkpoint.Seconds.Value) : "—";
        GhostTime = checkpoint.GhostSeconds.HasValue ? RunRaceViewModel.FormatTime(checkpoint.GhostSeconds.Value) : "—";
        Delta = !checkpoint.RouteMatches ? "Route differs" : checkpoint.DeltaSeconds.HasValue ? RunRaceViewModel.FormatDelta(checkpoint.DeltaSeconds.Value) : "—";
        Color = checkpoint.DeltaSeconds is < 0 ? Brushes.MediumSeaGreen : checkpoint.DeltaSeconds is > 0 ? Brushes.SandyBrown : Brushes.LightGray;
    }
    public string Number { get; }
    public string Boss { get; }
    public string Attempts { get; }
    public string LiveTime { get; }
    public string GhostTime { get; }
    public string Delta { get; }
    public IBrush Color { get; }
}
