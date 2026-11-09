using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogReview;
using SWTORCombatParser.Views.Battle_Review;

namespace SWTORCombatParser.ViewModels.BattleReview;

public static class ReviewSliderUpdates
{
    public static event Action<double> OnSliderUpdated = delegate { };
    public static void UpdateSlider(double value) => OnSliderUpdated(value);
}
public class AvailableEntity : ReactiveObject
{
    private bool _selected;
    public event Action<Entity, bool> EntitiySelectionUpdated = delegate { };
    public Entity Entity { get; set; }
    public string Detail => Entity.Name == "All" ? "All participants" : (Entity.IsCharacter ? "Player" : Entity.IsBoss ? "Boss" : "NPC") + " · ID " + Entity.LogId;
    public void DefaultSeleted() => SetSelected(true);
    public void DeselectAll() => SetSelected(false);
    public void SetSelected(bool value) => this.RaiseAndSetIfChanged(ref _selected, value, nameof(Selected));
    public bool Selected
    {
        get => _selected;
        set { if (_selected == value) return; SetSelected(value); EntitiySelectionUpdated(Entity, value); }
    }
}
public enum DisplayType
{
    [Description("All events")] All,
    Damage,
    [Description("Damage taken")] DamageTaken,
    Healing,
    [Description("Healing received")] HealingReceived,
    [Description("Death recap")] DeathRecap,
    [Description("Ability activations")] Abilities
}
public enum ParticipantRole { Either, Source, Target }
public class BattleReviewViewModel : ReactiveObject
{
    private Combat? _combat;
    private DisplayType _displayType;
    private ParticipantRole _participantRole;
    private string _filter = "";
    private string _entityFilter = "";
    private bool _regex;
    private bool _suppressUpdates;
    private readonly DispatcherTimer _filterTimer;
    private List<AvailableEntity> _entities = new();
    private (string Query, bool Regex, DisplayType Type, ParticipantRole Role, long[] Entities)? _previousSearch;

    public EventHistoryViewModel EventHistory { get; } = new();
    public EventHistoryView EventViewContent { get; }
    public Task LastUpdate { get; private set; } = Task.CompletedTask;
    // Direction belongs to the participant filter; damage/healing need only one
    // event category each. Legacy incoming categories remain for death review.
    public IReadOnlyList<DisplayType> AvailableDisplayTypes { get; } = new[] { DisplayType.All, DisplayType.Damage, DisplayType.Healing, DisplayType.DeathRecap, DisplayType.Abilities };
    public IReadOnlyList<ParticipantRole> ParticipantRoles { get; } = Enum.GetValues<ParticipantRole>();
    public ParticipantRole SelectedParticipantRole
    {
        get => _participantRole;
        set
        {
            if (_participantRole == value) return;
            this.RaiseAndSetIfChanged(ref _participantRole, value);
            EventHistory.SetParticipantRole(value); ApplySearch();
        }
    }
    public List<AvailableEntity> AvailableEntities { get => _entities; private set { this.RaiseAndSetIfChanged(ref _entities, value); this.RaisePropertyChanged(nameof(VisibleEntities)); } }
    public IEnumerable<AvailableEntity> VisibleEntities => AvailableEntities.Where(e => e.Entity.Name == "All" || e.Entity.Name?.Contains(EntityFilter, StringComparison.OrdinalIgnoreCase) == true);
    public string EntityFilter { get => _entityFilter; set { this.RaiseAndSetIfChanged(ref _entityFilter, value ?? ""); this.RaisePropertyChanged(nameof(VisibleEntities)); } }
    public string LogFilter
    {
        get => _filter;
        set
        {
            if (_filter == (value ?? "")) return;
            this.RaiseAndSetIfChanged(ref _filter, value ?? "");
            EventHistory.SetFilterText(_filter);
            ScheduleSearch();
        }
    }
    public bool UseRegex
    {
        get => _regex;
        set { if (_regex == value) return; this.RaiseAndSetIfChanged(ref _regex, value); EventHistory.SetRegexMode(value); ScheduleSearch(); }
    }
    public DisplayType SelectedDisplayType
    {
        get => _displayType;
        set { if (_displayType == value) return; this.RaiseAndSetIfChanged(ref _displayType, value); EventHistory.SetDisplayType(value); ApplySearch(); }
    }
    public bool CanReturnToResults => _previousSearch.HasValue;
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }
    public ReactiveCommand<string, Unit> FilterSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowContextCommand { get; }
    public ReactiveCommand<Unit, Unit> ReturnToResultsCommand { get; }

    public BattleReviewViewModel()
    {
        EventHistory.SetParticipantRole(ParticipantRole.Either);
        EventViewContent = new EventHistoryView(EventHistory);
        _filterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _filterTimer.Tick += (_, _) => ApplySearch();
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);
        FilterSelectionCommand = ReactiveCommand.Create<string>(FilterSelection);
        ShowContextCommand = ReactiveCommand.Create(ShowContext);
        ReturnToResultsCommand = ReactiveCommand.Create(ReturnToResults);
    }
    private void ScheduleSearch()
    {
        _filterTimer.Stop();
        if (!_suppressUpdates) _filterTimer.Start();
    }
    public void ApplySearch()
    {
        _filterTimer.Stop();
        if (!_suppressUpdates && _combat != null) LastUpdate = EventHistory.SelectCombat(_combat);
    }
    public void CombatSelected(Combat combat)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => CombatSelected(combat)); return; }
        var sameCombat = ReferenceEquals(_combat, combat);
        _combat = combat;
        if (!sameCombat) { _previousSearch = null; this.RaisePropertyChanged(nameof(CanReturnToResults)); }
        var entities = combat.AllEntities.Values.OrderByDescending(e => e.IsCharacter).ThenBy(e => e.Name).ThenBy(e => e.LogId).ToArray();
        if (!sameCombat || !AvailableEntities.Where(e => e.Entity.Name != "All").Select(e => e.Entity).SequenceEqual(entities))
        {
            var selectedNames = AvailableEntities.Where(e => e.Selected).Select(e => e.Entity.Name).ToHashSet();
            foreach (var old in AvailableEntities) old.EntitiySelectionUpdated -= UpdateSelectedEntities;
            var available = entities.Select(e => new AvailableEntity { Entity = e }).ToList();
            var all = new AvailableEntity { Entity = new Entity { Name = "All" } };
            available.Insert(0, all);
            foreach (var entity in available)
            {
                entity.SetSelected(selectedNames.Contains(entity.Entity.Name));
                entity.EntitiySelectionUpdated += UpdateSelectedEntities;
            }
            if (!available.Any(e => e.Selected)) all.SetSelected(true);
            AvailableEntities = available;
        }
        EventHistory.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
        ApplySearch();
    }
    public void Reset() => CombatSelected(new Combat());
    internal void ReleaseCombat()
    {
        _filterTimer.Stop(); _combat = null; _previousSearch = null;
        this.RaisePropertyChanged(nameof(CanReturnToResults));
        EventHistory.ClearCombat();
    }
    private void UpdateSelectedEntities(Entity entity, bool selected)
    {
        if (selected && entity.Name == "All")
            foreach (var item in AvailableEntities.Where(e => e.Entity.Name != "All")) item.SetSelected(false);
        else if (selected)
            AvailableEntities.First(e => e.Entity.Name == "All").SetSelected(false);
        EventHistory.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
        ApplySearch();
    }
    public void ClearFilters()
    {
        _suppressUpdates = true;
        try
        {
            LogFilter = ""; UseRegex = false; SelectedDisplayType = DisplayType.All; EntityFilter = ""; SelectedParticipantRole = ParticipantRole.Either;
            foreach (var entity in AvailableEntities) entity.SetSelected(entity.Entity.Name == "All");
            EventHistory.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
            _previousSearch = null; this.RaisePropertyChanged(nameof(CanReturnToResults));
        }
        finally { _suppressUpdates = false; }
        ApplySearch();
    }
    private void FilterSelection(string field)
    {
        var row = EventHistory.SelectedEntry;
        if (row == null) return;
        var text = field switch { "source" => row.Source, "target" => row.Target, "ability" => row.Ability, _ => null };
        if (string.IsNullOrEmpty(text)) return;
        var prefix = UseRegex ? "" : LogFilter.Trim();
        UseRegex = false;
        LogFilter = (prefix.Length > 0 ? prefix + " " : "") + field + ":" + LogSearchQuery.Quote(text);
        ApplySearch();
    }
    private void ShowContext()
    {
        var row = EventHistory.SelectedEntry;
        if (row == null) return;
        var previous = _previousSearch ?? (LogFilter, UseRegex, SelectedDisplayType, SelectedParticipantRole, AvailableEntities.Where(e => e.Selected).Select(e => e.Entity.LogId).ToArray());
        _suppressUpdates = true;
        try
        {
            UseRegex = false; SelectedDisplayType = DisplayType.All; SelectedParticipantRole = ParticipantRole.Either;
            foreach (var entity in AvailableEntities) entity.SetSelected(entity.Entity.Name == "All");
            EventHistory.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
            LogFilter = FormattableString.Invariant($"after:{row.ElapsedSeconds - 5:0.###} before:{row.ElapsedSeconds + 5:0.###}");
            _previousSearch = previous; this.RaisePropertyChanged(nameof(CanReturnToResults));
        }
        finally { _suppressUpdates = false; }
        ApplySearch();
        _ = RevealSelectionAfterUpdate();
    }
    private void ReturnToResults()
    {
        if (_previousSearch is not { } previous) return;
        _suppressUpdates = true;
        try
        {
            LogFilter = previous.Query; UseRegex = previous.Regex; SelectedDisplayType = previous.Type;
            SelectedParticipantRole = previous.Role;
            foreach (var entity in AvailableEntities) entity.SetSelected(previous.Entities.Contains(entity.Entity.LogId));
            EventHistory.SetViewableEntities(AvailableEntities.Where(e => e.Selected).Select(e => e.Entity).ToList());
            _previousSearch = null; this.RaisePropertyChanged(nameof(CanReturnToResults));
        }
        finally { _suppressUpdates = false; }
        ApplySearch();
        _ = RevealSelectionAfterUpdate();
    }
    private async Task RevealSelectionAfterUpdate()
    {
        var update = LastUpdate;
        await update;
        if (ReferenceEquals(update, LastUpdate)) EventViewContent.RevealSelection();
    }
}
