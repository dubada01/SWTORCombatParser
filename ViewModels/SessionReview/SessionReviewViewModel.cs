using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.SessionReview;
using SWTORCombatParser.Model.SessionReview;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.ViewModels.SessionReview;

public sealed record SessionChoice(EncounterCombat Encounter, string Label);
public sealed record SessionGraphChoice(SessionGraphMetric Metric, string Label);
public sealed record SessionRangeChoice(DateTime? Start, string Label);

public sealed class SessionPlayerChoice : ReactiveObject
{
    private bool _isCompared;
    private SessionPlayerInfo _info = new();
    private static readonly Lazy<Dictionary<Role, Bitmap>> Icons = new(() => new Dictionary<Role, Bitmap>
    {
        [Role.DPS] = Load("dpsIcon"), [Role.Healer] = Load("healingIcon"),
        [Role.Tank] = Load("tankIcon"), [Role.Unknown] = Load("question-mark")
    });
    private static Bitmap Load(string name) => new(AssetLoader.Open(new Uri($"avares://Orbs/resources/{name}.png")));
    public string Name { get; }
    public bool IsLocal { get; }
    public string DisplayName => Name + (IsLocal ? " · you" : "");
    public int ColorIndex { get; }
    public IBrush Color => SessionReviewViewModel.SeriesColor(ColorIndex);
    public Bitmap RoleIcon => Icons.Value.GetValueOrDefault(_info.Role) ?? Icons.Value[Role.Unknown];
    public string RoleDescription => _info.Role == Role.Unknown ? "Role not recorded" : $"{_info.Role} · {_info.Discipline}".TrimEnd(' ', '·');
    public bool IsCompared
    {
        get => _isCompared;
        set { if (value == _isCompared) return; this.RaiseAndSetIfChanged(ref _isCompared, value); ComparisonChanged?.Invoke(); }
    }
    public event Action? ComparisonChanged;
    public SessionPlayerChoice(string name, bool isLocal, int colorIndex)
    {
        Name = name; IsLocal = isLocal; ColorIndex = colorIndex; _isCompared = isLocal;
    }
    public void UpdateInfo(SessionPlayerInfo info)
    {
        if (_info == info) return;
        _info = info;
        this.RaisePropertyChanged(nameof(RoleIcon));
        this.RaisePropertyChanged(nameof(RoleDescription));
    }
}

public sealed class SessionCombatRow
{
    public int Number { get; init; }
    public string Name { get; init; } = "";
    public string Offset { get; init; } = "";
    public string Duration { get; init; } = "";
    public string Outcome { get; init; } = "";
    public IBrush OutcomeBrush { get; init; } = Brushes.Gray;
    public string Dps { get; init; } = "";
    public string Ehps { get; init; } = "";
    public string Dtps { get; init; } = "";
    public string Deaths { get; init; } = "";
}

public sealed class SessionReviewViewModel : ReactiveObject, IDisposable
{
    private readonly SessionReviewService _service;
    private readonly IDisposable _lease;
    private SessionChoice? _selectedSession;
    private SessionPlayerChoice? _selectedPlayer;
    private SessionGraphChoice _selectedMetric;
    private SessionRangeChoice? _selectedRange;
    private SessionReviewSnapshot? _filtered;
    private SessionGraphData? _graph;
    private bool _bossesOnly;
    private int _bucketSeconds = 5;
    private bool _updating;
    private EncounterCombat? _playerSession;

    public SessionPerformanceViewModel Summary { get; } = new();
    public ObservableCollection<SessionChoice> Sessions { get; } = new();
    public ObservableCollection<SessionPlayerChoice> Players { get; } = new();
    public ObservableCollection<SessionRangeChoice> GraphRanges { get; } = new();
    public IReadOnlyList<SessionGraphChoice> Metrics { get; } = new[]
    {
        new SessionGraphChoice(SessionGraphMetric.Dps, "DPS"),
        new SessionGraphChoice(SessionGraphMetric.DirectEhps, "Direct EHPS (excludes absorbs)"),
        new SessionGraphChoice(SessionGraphMetric.Dtps, "DTPS"),
        new SessionGraphChoice(SessionGraphMetric.HealthPercent, "HP %"),
        new SessionGraphChoice(SessionGraphMetric.Health, "HP")
    };
    public IReadOnlyList<int> BucketSizes { get; } = new[] { 1, 5, 10, 30 };
    public ObservableCollection<SessionCombatRow> Combats { get; } = new();
    public string Status => _service.Status;
    public string SessionTiming => _service.Snapshot is not { } snapshot ? "" :
        $"Whole session · {SessionPerformanceViewModel.Duration(snapshot.ElapsedSeconds)} recorded · {SessionPerformanceViewModel.Duration(snapshot.CombatSeconds)} in combat · {SessionPerformanceViewModel.Duration(snapshot.DowntimeSeconds)} between combats";
    public bool IsEmpty => !Summary.HasData;
    public string EmptyMessage => BossesOnly ? "No boss attempts in this session. Turn off Bosses only to see other combats." : "Load a previous log or start parsing to review a session.";
    public string SummaryScope => $"{SelectedPlayer?.Name ?? "Local player"} · {(BossesOnly ? "boss attempts" : "all combats")} · rates use fights where this player was recorded";
    public string ComparisonLabel => $"{Players.Count(p => p.IsCompared)} of {Players.Count} players shown";
    public string GraphNote => IsRateMetric
        ? $"{BucketSeconds}s averages within each combat · gaps keep travel and excluded combats visible · hover for values"
        : "HP is the last recorded observation each second · gaps mean unknown health · hover for values";
    public bool IsRateMetric => SelectedMetric.Metric is not (SessionGraphMetric.Health or SessionGraphMetric.HealthPercent);
    public SessionGraphData? Graph { get => _graph; private set => this.RaiseAndSetIfChanged(ref _graph, value); }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> CompareAllCommand { get; }
    public ReactiveCommand<Unit, Unit> CompareNoneCommand { get; }
    public ReactiveCommand<Unit, Unit> CompareSelfCommand { get; }

    public SessionChoice? SelectedSession
    {
        get => _selectedSession;
        set { this.RaiseAndSetIfChanged(ref _selectedSession, value); if (!_updating && value is not null) _service.SelectEncounter(value.Encounter); }
    }
    public SessionPlayerChoice? SelectedPlayer
    {
        get => _selectedPlayer;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedPlayer, value);
            if (_updating) return;
            if (value is not null) value.IsCompared = true;
            UpdateMetrics();
        }
    }
    public bool BossesOnly
    {
        get => _bossesOnly;
        set { this.RaiseAndSetIfChanged(ref _bossesOnly, value); if (!_updating) UpdateMetrics(); }
    }
    public SessionGraphChoice SelectedMetric
    {
        get => _selectedMetric;
        set
        {
            if (value is null) return;
            this.RaiseAndSetIfChanged(ref _selectedMetric, value);
            this.RaisePropertyChanged(nameof(IsRateMetric));
            UpdateGraph();
        }
    }
    public SessionRangeChoice? SelectedRange
    {
        get => _selectedRange;
        set { this.RaiseAndSetIfChanged(ref _selectedRange, value); if (!_updating) UpdateGraph(); }
    }
    public int BucketSeconds
    {
        get => _bucketSeconds;
        set { this.RaiseAndSetIfChanged(ref _bucketSeconds, value); UpdateGraph(); }
    }

    public SessionReviewViewModel(SessionReviewService service)
    {
        _service = service;
        _selectedMetric = Metrics[0];
        RefreshCommand = ReactiveCommand.Create(() => service.Refresh());
        CompareAllCommand = ReactiveCommand.Create(() => SetComparisons(_ => true));
        CompareNoneCommand = ReactiveCommand.Create(() => SetComparisons(_ => false));
        CompareSelfCommand = ReactiveCommand.Create(() => SetComparisons(p => p.IsLocal));
        _service.Changed += Update;
        _lease = service.Acquire();
        Update();
    }

    private void SetComparisons(Func<SessionPlayerChoice, bool> predicate)
    {
        _updating = true;
        foreach (var player in Players) player.IsCompared = predicate(player);
        _updating = false;
        UpdateGraph();
    }

    private void Update()
    {
        _updating = true;
        if (!Sessions.Select(s => s.Encounter).SequenceEqual(_service.Encounters))
        {
            Sessions.Clear();
            foreach (var encounter in _service.Encounters)
            {
                var start = encounter.EncounterCombats.Select(c => c.CombatStartTime).DefaultIfEmpty().Min();
                Sessions.Add(new SessionChoice(encounter, $"{encounter.Info?.NamePlus} · {start:g}"));
            }
        }
        SelectedSession = Sessions.FirstOrDefault(s => ReferenceEquals(s.Encounter, _service.SelectedEncounter));
        var snapshot = _service.Snapshot;
        var names = snapshot?.Combats.SelectMany(c => c.Players.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        if (!Players.Select(p => p.Name).SequenceEqual(names) || !ReferenceEquals(_playerSession, _service.SelectedEncounter))
        {
            var sameSession = ReferenceEquals(_playerSession, _service.SelectedEncounter);
            var selected = sameSession ? SelectedPlayer?.Name : null;
            var compared = Players.ToDictionary(p => p.Name, p => p.IsCompared, StringComparer.OrdinalIgnoreCase);
            foreach (var player in Players) player.ComparisonChanged -= UpdateGraph;
            Players.Clear();
            for (var i = 0; i < names.Length; i++)
            {
                var player = new SessionPlayerChoice(names[i], snapshot!.Combats.Any(c => c.LocalPlayers.Contains(names[i], StringComparer.OrdinalIgnoreCase)), i);
                if (sameSession && compared.TryGetValue(player.Name, out var wasCompared)) player.IsCompared = wasCompared;
                player.ComparisonChanged += UpdateGraph;
                Players.Add(player);
            }
            SelectedPlayer = Players.FirstOrDefault(p => p.Name == selected) ?? Players.FirstOrDefault(p => p.IsLocal) ?? Players.FirstOrDefault();
            _playerSession = _service.SelectedEncounter;
        }
        foreach (var player in Players)
        {
            var info = snapshot?.Combats.OrderByDescending(c => c.Start).Select(c => c.PlayerInfo.GetValueOrDefault(player.Name))
                .FirstOrDefault(i => i is not null && i.Role != Role.Unknown);
            player.UpdateInfo(info ?? new SessionPlayerInfo());
        }
        _updating = false;
        UpdateMetrics();
        this.RaisePropertyChanged(nameof(Status));
    }

    private void UpdateMetrics()
    {
        _filtered = _service.Snapshot?.FilterBosses(BossesOnly);
        var player = SelectedPlayer?.Name;
        Summary.Update(_filtered, player);
        Combats.Clear();
        _updating = true;
        var previousRange = SelectedRange?.Start;
        var ranges = new List<SessionRangeChoice> { new(null, "Whole session") };
        if (_filtered is not null)
        {
            foreach (var combat in _filtered.Combats)
            {
                var index = _service.Snapshot!.Combats.ToList().IndexOf(combat) + 1;
                var metrics = combat.ForPlayer(player);
                var available = combat.HasData && combat.HasPlayer(player);
                string Rate(double value) => available ? SessionPerformanceViewModel.Rate(value, combat.DurationSeconds).ToString("N0") : "—";
                var outcome = combat.Outcome switch { SessionCombatOutcome.Kill => "Kill", SessionCombatOutcome.Wipe => "Wipe", SessionCombatOutcome.InProgress => "Live", SessionCombatOutcome.PvP => "PvP", _ => "Combat" };
                Combats.Add(new SessionCombatRow
                {
                    Number = index, Name = combat.Name, Offset = "+" + SessionPerformanceViewModel.Duration((combat.Start - _filtered.Start).TotalSeconds),
                    Duration = SessionPerformanceViewModel.Duration(combat.DurationSeconds), Outcome = outcome, OutcomeBrush = OutcomeColor(combat.Outcome),
                    Dps = Rate(metrics.Damage), Ehps = Rate(metrics.EffectiveHealing), Dtps = Rate(metrics.DamageTaken), Deaths = available ? metrics.Deaths.ToString() : "—"
                });
                ranges.Add(new SessionRangeChoice(combat.Start, $"{index:00} · {combat.Name} · {outcome}"));
            }
        }
        if (!GraphRanges.SequenceEqual(ranges))
        {
            GraphRanges.Clear();
            foreach (var range in ranges) GraphRanges.Add(range);
        }
        SelectedRange = GraphRanges.FirstOrDefault(r => r.Start == previousRange) ?? GraphRanges.FirstOrDefault();
        _updating = false;
        UpdateGraph();
        foreach (var property in new[] { nameof(IsEmpty), nameof(EmptyMessage), nameof(SummaryScope), nameof(SessionTiming) }) this.RaisePropertyChanged(property);
    }

    private void UpdateGraph()
    {
        if (_updating) return;
        var snapshot = _filtered;
        if (snapshot is not null && SelectedRange?.Start is { } start)
        {
            var combats = snapshot.Combats.Where(c => c.Start == start).ToArray();
            snapshot = snapshot with { Combats = combats, RangeStart = null, RangeEnd = null };
        }
        var graph = snapshot is null ? null : SessionGraphBuilder.Build(snapshot, Players.Where(p => p.IsCompared).Select(p => p.Name), SelectedMetric.Metric, BucketSeconds);
        Graph = graph is null ? null : graph with { Series = graph.Series.Select(s => s with { ColorIndex = Players.First(p => p.Name == s.Player).ColorIndex }).ToArray() };
        this.RaisePropertyChanged(nameof(ComparisonLabel));
        this.RaisePropertyChanged(nameof(GraphNote));
    }

    private static readonly string[] Palette = { "#7DE0AE", "#8AE3EA", "#E5B482", "#C6A5E8", "#F28C93", "#C6D87A", "#81ACE8", "#DFAACA", "#BBD0C4", "#DBD297", "#ACB2E5", "#E89475", "#73C9CA", "#D1BB9C", "#B9DE91", "#B9B2A9" };
    public static IBrush SeriesColor(int index) => Brush.Parse(Palette[Math.Abs(index) % Palette.Length]);
    public static IBrush OutcomeColor(SessionCombatOutcome outcome) => outcome switch
    {
        SessionCombatOutcome.Kill => Brush.Parse("#7DE0AE"), SessionCombatOutcome.Wipe => Brush.Parse("#F28C93"),
        SessionCombatOutcome.PvP => Brush.Parse("#C6A5E8"), SessionCombatOutcome.InProgress => Brush.Parse("#D9BB7B"), _ => Brush.Parse("#889D93")
    };

    public void Dispose()
    {
        _service.Changed -= Update;
        foreach (var player in Players) player.ComparisonChanged -= UpdateGraph;
        _lease.Dispose();
        RefreshCommand.Dispose(); CompareAllCommand.Dispose(); CompareNoneCommand.Dispose(); CompareSelfCommand.Dispose();
    }
}
