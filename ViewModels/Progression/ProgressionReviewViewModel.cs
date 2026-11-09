using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatCaching;
using SWTORCombatParser.Model.Progression;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.ViewModels.Progression;

public enum ProgressionScope { CurrentRaid, SavedHistory }

public sealed record ProgressionPullRow(ProgressionPullSummary Summary, int Number, double ScaleSeconds)
{
    public string Boss => Summary.Metadata.Boss;
    public string Started => Summary.Metadata.Start.ToString("yyyy-MM-dd HH:mm");
    public string Duration => ProgressionTime.Format(Summary.Metadata.DurationSeconds);
    public double DurationFraction => ScaleSeconds > 0 ? Summary.Metadata.DurationSeconds / ScaleSeconds : 0;
    public double? FirstDeathSeconds => Summary.FirstDeath is { } death ? (death.Time - Summary.Metadata.Start).TotalSeconds : null;
    public double FirstDeathFraction => ScaleSeconds > 0 && FirstDeathSeconds is { } seconds ? seconds / ScaleSeconds : -1;
    public string FirstDeath => Summary.State != ProgressionDataState.Available ? "—" : Summary.FirstDeath is { } death
        ? ProgressionTime.Format((death.Time - Summary.Metadata.Start).TotalSeconds, precise: true) : "None";
    public string FirstDeathPhase => Summary.FirstDeath?.Phase ?? "No phase data";
    public string Phase => Summary.LastPhase ?? (Summary.State == ProgressionDataState.Available ? "No phase data" : "—");
    public string Outcome => Summary.Metadata.WasKilled ? "Kill" : "No clear";
    public IBrush OutcomeBrush => Summary.Metadata.WasKilled ? Brushes.MediumAquamarine : Brushes.Salmon;
    public int? DeathCountValue => Summary.State == ProgressionDataState.Available ? Summary.Deaths.Count : null;
    public string DeathCount => DeathCountValue?.ToString() ?? "—";
}

public sealed record ProgressionDeathRow(ProgressionDeath Death, DateTime Start)
{
    public string Elapsed => ProgressionTime.Format((Death.Time - Start).TotalSeconds, precise: true);
    public string Player => Death.Player;
    public string Phase => Death.Phase ?? "No phase data";
    public string LastHit => Death.IsExplicitReset ? "Stuck / reset" : Death.LastHit?.Ability ?? "Unknown";
    public string Source => Death.LastHit?.Source ?? "—";
    public string Amount => Death.LastHit?.Amount.ToString("N0") ?? "—";
}

public sealed record ProgressionLeadupRow(ProgressionRecapEvent Event, DateTime DeathTime)
{
    public string Offset => $"−{(DeathTime - Event.Time).TotalSeconds:F2}s";
    public string Kind => Event.Kind == ProgressionEventKind.Damage ? "Damage" : "Heal";
    public IBrush KindBrush => Event.Kind == ProgressionEventKind.Damage ? Brushes.Salmon : Brushes.MediumAquamarine;
    public string Ability => Event.Ability ?? "Unknown";
    public string Source => Event.Source ?? "Unknown";
    public string Amount => Event.Amount.ToString("N0");
}

internal static class ProgressionTime
{
    public static string Format(double seconds, bool precise = false)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(long)time.TotalMinutes}:{time.Seconds:D2}" + (precise ? $".{time.Milliseconds / 100}" : "");
    }
}

public sealed class ProgressionReviewViewModel : ReactiveObject, IDisposable
{
    private const string AllBosses = "All bosses";
    private const string Unassigned = "Unassigned";
    private readonly ProgressionReviewService _service;
    private ProgressionScope _scope;
    private ProgressionRaidChoice? _selectedRaid;
    private ProgressionCohort? _selectedCohort;
    private string _selectedBoss = AllBosses;
    private string _selectedTeam = Unassigned;
    private string _teamName = "";
    private string? _teamRaidId;
    private string? _savedTeamName;
    private ProgressionPullRow? _selectedPull;
    private ProgressionDeathRow? _selectedDeath;
    private bool _followingLatest = true;
    private bool _followRaid = true;
    private bool _historyTeamChosen;
    private bool _refreshing;
    private bool _savingTeam;
    private bool _detailLoading;
    private bool _disposed;
    private string? _detailError;

    public IReadOnlyList<ProgressionScope> Scopes { get; } = Enum.GetValues<ProgressionScope>();
    public IReadOnlyList<ProgressionRaidChoice> Raids { get; private set; } = Array.Empty<ProgressionRaidChoice>();
    public IReadOnlyList<ProgressionCohort> Cohorts { get; private set; } = Array.Empty<ProgressionCohort>();
    public IReadOnlyList<string> Teams { get; private set; } = new[] { Unassigned };
    public IReadOnlyList<string> Bosses { get; private set; } = new[] { AllBosses };
    public IReadOnlyList<ProgressionPullRow> Pulls { get; private set; } = Array.Empty<ProgressionPullRow>();
    public IReadOnlyList<ProgressionDeathRow> Deaths { get; private set; } = Array.Empty<ProgressionDeathRow>();
    public IReadOnlyList<ProgressionLeadupRow> Leadup { get; private set; } = Array.Empty<ProgressionLeadupRow>();
    public ProgressionScope Scope
    {
        get => _scope;
        set { if (!_refreshing && _scope != value) { _scope = value; Refresh(); } }
    }
    public bool IsCurrentRaid => Scope == ProgressionScope.CurrentRaid;
    public bool IsSavedHistory => !IsCurrentRaid;
    public bool IsIndexing => _service.IsIndexing;
    public bool IsEmpty => Pulls.Count == 0;
    public bool CanEditTeam => IsCurrentRaid && SelectedRaid is { IsReplay: false } && !_savingTeam;
    public bool CanSaveTeam => CanEditTeam && TeamName.Trim().Length <= 64 &&
        !string.Equals(TeamName.Trim(), Unassigned, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(TeamName.Trim(), _savedTeamName ?? "", StringComparison.Ordinal);
    public bool CanOpenDetailedReview => !_detailLoading && SelectedPull != null &&
        _service.FindLoadedPull(SelectedPull.Summary.Metadata.Id) is { } row &&
        (row.Combat != null || !string.IsNullOrEmpty(row.CombatCachePath));
    public string DetailButton => _detailLoading ? "Loading…" : "Detailed analysis";
    public string DetailToolTip => CanOpenDetailedReview ? "Open damage, 30-second recap and death analysis for this pull."
        : "Load the source log to open full combat analysis.";

    public ProgressionRaidChoice? SelectedRaid
    {
        get => _selectedRaid;
        set
        {
            if (_refreshing || value == null || value == _selectedRaid) return;
            _selectedRaid = value;
            _followRaid = false;
            _selectedCohort = null;
            _followingLatest = true;
            Refresh();
        }
    }
    public ProgressionCohort? SelectedCohort
    {
        get => _selectedCohort;
        set
        {
            if (_refreshing || value == null || value == _selectedCohort) return;
            _selectedCohort = value;
            _followingLatest = true;
            Refresh();
        }
    }
    public string SelectedTeam
    {
        get => _selectedTeam;
        set
        {
            if (_refreshing || string.IsNullOrEmpty(value) || value == _selectedTeam) return;
            _selectedTeam = value;
            _historyTeamChosen = true;
            _followingLatest = true;
            Refresh();
        }
    }
    public string SelectedBoss
    {
        get => _selectedBoss;
        set
        {
            if (_refreshing || string.IsNullOrEmpty(value) || value == _selectedBoss) return;
            _selectedBoss = value;
            _followingLatest = true;
            Refresh();
        }
    }
    public string TeamName
    {
        get => _teamName;
        set
        {
            this.RaiseAndSetIfChanged(ref _teamName, value ?? "");
            this.RaisePropertyChanged(nameof(CanSaveTeam));
        }
    }
    public ProgressionPullRow? SelectedPull
    {
        get => _selectedPull;
        set
        {
            if (_refreshing || value == null || ReferenceEquals(value, _selectedPull)) return;
            _selectedPull = value;
            _followingLatest = false;
            if (IsCurrentRaid) _followRaid = false;
            _detailError = null;
            RefreshAttempt();
            UpdatePriority();
            this.RaisePropertyChanged(nameof(FollowLabel));
        }
    }
    public ProgressionDeathRow? SelectedDeath
    {
        get => _selectedDeath;
        set
        {
            if (_refreshing || value == null || ReferenceEquals(value, _selectedDeath)) return;
            _selectedDeath = value;
            RefreshLeadup();
        }
    }

    public string Status { get; private set; } = "";
    public string Overview { get; private set; } = "";
    public string EarlyHits { get; private set; } = "—";
    public string EndingPhases { get; private set; } = "—";
    public string FollowLabel => _followingLatest ? "Following latest" : "Jump to latest";
    public string EmptyMessage => IsCurrentRaid ? "Load a raid log or complete a boss pull." :
        IsIndexing ? "Reading saved pulls…" : "No saved pulls for this team.";
    public string AttemptTitle => SelectedPull is { } pull ? $"Pull {pull.Number}  ·  {pull.Boss}" : "Pull recap";
    public string AttemptSummary => SelectedPull is not { } pull ? "" : $"{pull.Outcome}  ·  {pull.Duration}  ·  " +
        (pull.Summary.EndingPhase is { } phase ? $"Ended in {phase}" : pull.Summary.LastPhase != null ? "No phase recorded at end" : pull.Phase);
    public string DeathEmptyMessage => SelectedPull?.Summary.State switch
    {
        ProgressionDataState.Pending => "Reading this pull…",
        ProgressionDataState.Unavailable => "Combat events unavailable. Reload the source log to retry.",
        ProgressionDataState.Available => "No player deaths recorded.",
        _ => "Select a pull to review it."
    };
    public string LeadupTitle => SelectedDeath is { } death ? $"Lead-up  ·  {death.Player}" : "Lead-up";
    public string LeadupEmptyMessage => SelectedDeath == null ? "Select a death to see its lead-up." : "No incoming damage or healing recorded.";
    public bool HasNoDeaths => Deaths.Count == 0;
    public bool HasNoLeadup => Leadup.Count == 0;
    public ReactiveCommand<Unit, Unit> SaveTeamCommand { get; }
    public ReactiveCommand<Unit, Unit> FollowLatestCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDetailedReviewCommand { get; }
    public event Action<Combat>? DetailedReviewRequested;

    public ProgressionReviewViewModel(ProgressionReviewService service)
    {
        _service = service;
        _service.Changed += Refresh;
        SaveTeamCommand = ReactiveCommand.CreateFromTask(SaveTeamAsync, this.WhenAnyValue(vm => vm.CanSaveTeam));
        FollowLatestCommand = ReactiveCommand.Create(() => { _followingLatest = true; Refresh(); });
        OpenDetailedReviewCommand = ReactiveCommand.CreateFromTask(OpenDetailedReviewAsync, this.WhenAnyValue(vm => vm.CanOpenDetailedReview));
    }

    public void Open(PastCombat? focus = null)
    {
        _refreshing = true;
        var focusId = _service.Activate(focus);
        _refreshing = false;
        _scope = ProgressionScope.CurrentRaid;
        var target = _service.CurrentPulls.FirstOrDefault(pull => pull.Metadata.Id == focusId);
        _selectedRaid = target == null ? _service.Raids.FirstOrDefault() : _service.Raids.FirstOrDefault(raid => raid.Id == target.Metadata.RaidId);
        _selectedCohort = target?.Metadata.Cohort;
        _selectedBoss = target?.Metadata.Boss ?? AllBosses;
        _followingLatest = target == null;
        _followRaid = target == null;
        _selectedPull = target == null ? null : new ProgressionPullRow(target, 0, 0);
        Refresh();
    }

    private void Refresh()
    {
        if (_disposed || _refreshing) return;
        _refreshing = true;
        try
        {
            ReplaceList(Raids, _service.Raids, values => Raids = values, nameof(Raids));
            var previousRaidId = _selectedRaid?.Id;
            _selectedRaid = (_followRaid ? null : Raids.FirstOrDefault(raid => raid.Id == previousRaidId)) ?? Raids.FirstOrDefault();
            var teamNames = _service.SavedRaids.Select(raid => raid.Team).OfType<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).Prepend(Unassigned).ToArray();
            ReplaceList(Teams, teamNames, values => Teams = values, nameof(Teams));
            var savedTeam = _service.SavedRaids.FirstOrDefault(raid => raid.Raid.Id == SelectedRaid?.Id)?.Team;
            if (!_historyTeamChosen)
            {
                if (savedTeam != null) _selectedTeam = savedTeam;
                else if (SelectedRaid == null && !_service.SavedRaids.Any(raid => raid.Team == null && raid.Pulls.Count > 0))
                    _selectedTeam = _service.SavedRaids.Where(raid => raid.Pulls.Count > 0).MaxBy(raid => raid.Raid.Start)?.Team ?? Unassigned;
            }
            _selectedTeam = Teams.FirstOrDefault(team => string.Equals(team, SelectedTeam, StringComparison.OrdinalIgnoreCase)) ?? Unassigned;
            if (_teamRaidId != SelectedRaid?.Id || _savedTeamName != savedTeam)
            {
                _teamRaidId = SelectedRaid?.Id;
                _savedTeamName = savedTeam;
                TeamName = savedTeam ?? "";
            }

            var scopePulls = (IsCurrentRaid
                ? _service.CurrentPulls.Where(pull => pull.Metadata.RaidId == SelectedRaid?.Id)
                : _service.SavedRaids.Where(raid => TeamMatches(raid.Team)).SelectMany(raid => raid.Pulls)).ToArray();
            var cohorts = scopePulls.Select(pull => pull.Metadata.Cohort).Distinct().OrderBy(cohort => cohort.Label).ToArray();
            ReplaceList(Cohorts, cohorts, values => Cohorts = values, nameof(Cohorts));
            _selectedCohort = (IsCurrentRaid && _followRaid && previousRaidId != _selectedRaid?.Id ? null
                : Cohorts.FirstOrDefault(cohort => cohort == SelectedCohort))
                ?? scopePulls.MaxBy(pull => pull.Metadata.Start)?.Metadata.Cohort;
            var cohortPulls = scopePulls.Where(pull => pull.Metadata.Cohort == SelectedCohort).OrderBy(pull => pull.Metadata.Start).ToArray();
            var bosses = cohortPulls.Select(pull => pull.Metadata.Boss).Distinct(StringComparer.Ordinal).Prepend(AllBosses).ToArray();
            ReplaceList(Bosses, bosses, values => Bosses = values, nameof(Bosses));
            if (!Bosses.Contains(SelectedBoss)) _selectedBoss = AllBosses;

            var visible = cohortPulls.Where(pull => SelectedBoss == AllBosses || pull.Metadata.Boss == SelectedBoss)
                .Select(_service.WithCurrentPhases).ToArray();
            var scaleSeconds = visible.Length == 0 ? 0 : visible.Max(pull => pull.Metadata.DurationSeconds);
            var rows = visible.Select((pull, index) => new ProgressionPullRow(pull, index + 1, scaleSeconds)).ToArray();
            ReplaceList(Pulls, rows, values => Pulls = values, nameof(Pulls));
            _selectedPull = (_followingLatest ? null : Pulls.FirstOrDefault(row => row.Summary.Metadata.Id == _selectedPull?.Summary.Metadata.Id))
                ?? Pulls.LastOrDefault();

            var kills = visible.Count(pull => pull.Metadata.WasKilled);
            Overview = $"{visible.Length} {(visible.Length == 1 ? "pull" : "pulls")}  ·  {kills} {(kills == 1 ? "kill" : "kills")}  ·  {visible.Length - kills} no clear";
            EarlyHits = SelectedBoss == AllBosses ? "Choose a boss to compare" : FormatPatterns(ProgressionPatterns.EarlyHits(visible));
            EndingPhases = SelectedBoss == AllBosses ? "Choose a boss to compare" : FormatPatterns(ProgressionPatterns.EndingPhases(visible));
            Status = _service.Warning ?? (_service.HasHistoryErrors ? "Some saved raid files could not be read." : StatusFor(visible));
            RefreshAttempt();
            Notify(nameof(Scope), nameof(IsCurrentRaid), nameof(IsSavedHistory), nameof(SelectedRaid), nameof(SelectedTeam),
                nameof(SelectedCohort), nameof(SelectedBoss), nameof(IsIndexing), nameof(IsEmpty), nameof(EmptyMessage),
                nameof(CanEditTeam), nameof(CanSaveTeam), nameof(Overview), nameof(EarlyHits), nameof(EndingPhases), nameof(Status), nameof(FollowLabel));
            UpdatePriority();
        }
        finally { _refreshing = false; }
    }

    private bool TeamMatches(string? team) => SelectedTeam == Unassigned ? string.IsNullOrWhiteSpace(team)
        : string.Equals(team, SelectedTeam, StringComparison.OrdinalIgnoreCase);

    private string StatusFor(ProgressionPullSummary[] visible)
    {
        if (_detailError != null) return _detailError;
        if (IsIndexing) return "Indexing boss pulls…";
        var missing = visible.Count(pull => pull.State == ProgressionDataState.Unavailable);
        if (missing > 0) return $"Combat events unavailable for {missing} {(missing == 1 ? "pull" : "pulls")}.";
        if (IsCurrentRaid) return SelectedRaid?.IsReplay == true ? "Replay session" : "Completed live pulls are saved automatically.";
        return "Saved on this device";
    }

    private static string FormatPatterns(ProgressionPattern[] patterns) => patterns.Length == 0 ? "No repeats recorded" :
        string.Join("  ·  ", patterns.Take(3).Select(pattern => $"{pattern.Name} ×{pattern.PullCount}"));

    private void RefreshAttempt()
    {
        var wasRefreshing = _refreshing;
        _refreshing = true;
        try
        {
            var deaths = SelectedPull is { } pull ? pull.Summary.Deaths.Select(death => new ProgressionDeathRow(death, pull.Summary.Metadata.Start)).ToArray()
                : Array.Empty<ProgressionDeathRow>();
            var previous = SelectedDeath;
            ReplaceList(Deaths, deaths, values => Deaths = values, nameof(Deaths));
            _selectedDeath = (previous == null ? null : Deaths.FirstOrDefault(row => row.Start == previous.Start &&
                row.Death.Player == previous.Death.Player && row.Death.Time == previous.Death.Time)) ?? Deaths.FirstOrDefault();
            RefreshLeadup();
            Notify(nameof(SelectedPull), nameof(AttemptTitle), nameof(AttemptSummary), nameof(DeathEmptyMessage), nameof(HasNoDeaths),
                nameof(CanOpenDetailedReview), nameof(DetailToolTip), nameof(DetailButton));
        }
        finally { _refreshing = wasRefreshing; }
    }

    private void RefreshLeadup()
    {
        var leadup = SelectedDeath is { } death ? death.Death.Leadup.Select(item => new ProgressionLeadupRow(item, death.Death.Time)).ToArray()
            : Array.Empty<ProgressionLeadupRow>();
        ReplaceList(Leadup, leadup, values => Leadup = values, nameof(Leadup));
        Notify(nameof(SelectedDeath), nameof(LeadupTitle), nameof(LeadupEmptyMessage), nameof(HasNoLeadup));
    }

    private void UpdatePriority() => _service.Prioritize(SelectedPull?.Summary.Metadata.Id, SelectedRaid?.Id);

    private async Task SaveTeamAsync()
    {
        if (!CanSaveTeam || SelectedRaid is not { } raid) return;
        _savingTeam = true;
        Notify(nameof(CanEditTeam), nameof(CanSaveTeam));
        try
        {
            var input = TeamName.Trim();
            var name = Teams.FirstOrDefault(team => string.Equals(team, input, StringComparison.OrdinalIgnoreCase)) ?? input;
            await _service.AssignTeamAsync(raid, name);
        }
        finally { _savingTeam = false; Refresh(); }
    }

    private async Task OpenDetailedReviewAsync()
    {
        if (!CanOpenDetailedReview || SelectedPull is not { } selection ||
            _service.FindLoadedPull(selection.Summary.Metadata.Id) is not { } row) return;
        _detailLoading = true;
        _detailError = null;
        Notify(nameof(CanOpenDetailedReview), nameof(DetailButton));
        try
        {
            var resident = row.Combat;
            var path = row.CombatCachePath;
            var combat = resident ?? await Task.Run(() => CombatDiskCacheManager.LoadCombat(path));
            if (!_disposed && SelectedPull?.Summary.Metadata.Id == selection.Summary.Metadata.Id && combat.AllLogs.Count > 0)
                DetailedReviewRequested?.Invoke(combat);
        }
        catch (Exception error) when (ProgressionHistoryStore.IsFileError(error) || error is NotSupportedException)
        {
            _detailError = "Full combat analysis is unavailable. Reload the source log to retry.";
        }
        finally { _detailLoading = false; Refresh(); }
    }

    private void ReplaceList<T>(IReadOnlyList<T> old, IReadOnlyList<T> updated, Action<IReadOnlyList<T>> assign, string property)
    {
        if (old.SequenceEqual(updated)) return;
        assign(updated);
        this.RaisePropertyChanged(property);
    }

    private void Notify(params string[] properties)
    {
        foreach (var property in properties) this.RaisePropertyChanged(property);
    }

    public void Dispose()
    {
        _disposed = true;
        _service.Changed -= Refresh;
        _service.Deactivate();
        SaveTeamCommand.Dispose();
        FollowLatestCommand.Dispose();
        OpenDetailedReviewCommand.Dispose();
        DetailedReviewRequested = null;
    }
}
