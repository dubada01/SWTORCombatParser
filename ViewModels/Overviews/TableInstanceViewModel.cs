using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Overviews;

public enum OverviewDataType { Damage, Healing, DamageTaken, HealingReceived, Threat }
public enum SortingOption
{
    [Description("Ability")] ByAbility,
    [Description("Source")] BySource,
    [Description("Target")] ByTarget
}

public class CombatInfoInstance : ReactiveObject
{
    private Bitmap? _icon;
    private Task? _iconTask;
    internal ParsedLogEntry? IconLog;
    internal SortingOption IconGrouping;
    public string SortItem { get; set; } = "";
    public double SumTotal { get; set; }
    public double RateDouble { get; set; }
    public double Rate => RateDouble;
    public double Total { get; set; }
    public double PercentOfTotal => SumTotal == 0 ? 0 : Total / SumTotal * 100;
    public string PercentOfTotalStr => $"{PercentOfTotal:0.#}%";
    public double Average { get; set; }
    public int Count { get; set; }
    public double CritPercent { get; set; }
    public double MaxCrit { get; set; }
    public double Max { get; set; }
    public string Type { get; set; } = "—";
    public Bitmap? Icon { get => _icon; set => this.RaiseAndSetIfChanged(ref _icon, value); }

    internal void UpdateValues(CombatInfoInstance row)
    {
        if (SumTotal == row.SumTotal && RateDouble == row.RateDouble && Total == row.Total && Average == row.Average &&
            Count == row.Count && CritPercent == row.CritPercent && MaxCrit == row.MaxCrit && Max == row.Max && Type == row.Type) return;
        SumTotal = row.SumTotal; RateDouble = row.RateDouble; Total = row.Total; Average = row.Average;
        Count = row.Count; CritPercent = row.CritPercent; MaxCrit = row.MaxCrit; Max = row.Max; Type = row.Type;
        this.RaisePropertyChanged("");
    }

    public Task EnsureIconAsync() => _iconTask ??= LoadIcon();
    private async Task LoadIcon()
    {
        var log = IconLog;
        if (log == null) return;
        try
        {
            var icon = IconGrouping == SortingOption.ByAbility ? await IconGetter.GetIconPathForLog(log) :
                IconFactory.GetClassIcon((IconGrouping == SortingOption.BySource ? log.SourceInfo.Class : log.TargetInfo.Class)?.Discipline);
            await Dispatcher.UIThread.InvokeAsync(() => Icon = icon);
        }
        catch (Exception error) { System.Diagnostics.Debug.WriteLine(error.Message); }
    }
}

public class TableInstanceViewModel : OverviewInstanceViewModel
{
    private SortingOption _sorting;
    private bool _active = true;
    private bool _loading;
    private string _filter = "";
    private int _version;
    private CancellationTokenSource? _pending;
    private (Combat Combat, Entity Entity, int Count, int Duration, SortingOption Group)? _built;
    private (Combat Combat, Entity Entity, int Count, int Duration, SortingOption Group)? _building;
    private readonly List<CombatInfoInstance> _rows = new();
    public ObservableCollection<CombatInfoInstance> DataToView { get; } = new();
    public ObservableCollection<CombatInfoInstance> Totals { get; } = new();
    public CombatInfoInstance? Overall => Totals.FirstOrDefault();
    public bool HasTotals => Overall != null;
    public Task LastUpdate { get; private set; } = Task.CompletedTask;
    public bool IsLoading { get => _loading; private set { this.RaiseAndSetIfChanged(ref _loading, value); this.RaisePropertyChanged(nameof(IsEmpty)); } }
    public bool IsEmpty => !IsLoading && DataToView.Count == 0;
    public string EmptyMessage => SelectedCombat == null ? "Select a combat to review" : _filter.Length > 0 ? "No rows match this search" : "No events for this participant";
    public string Summary => Totals.FirstOrDefault() is { } row ? $"{row.Total:N0} total   ·   {row.RateDouble:N0}/s   ·   {row.Count:N0} events" : "";
    public string ResultCount => $"{DataToView.Count:N0} / {_rows.Count:N0}";
    public string FilterText { get => _filter; set { this.RaiseAndSetIfChanged(ref _filter, value ?? ""); ApplyFilter(); } }
    public string SelectedSortName => _sorting switch { SortingOption.BySource => "Source", SortingOption.ByTarget => "Target", _ => "Ability" };
    public override SortingOption SortingOption
    {
        get => _sorting;
        set { if (_sorting == value) return; this.RaiseAndSetIfChanged(ref _sorting, value); this.RaisePropertyChanged(nameof(SelectedSortName)); Update(); }
    }
    public TableInstanceViewModel(OverviewDataType type) : base(type) { }

    internal void SetContext(Combat combat, Entity entity, SortingOption group, bool active)
    {
        var changed = !ReferenceEquals(SelectedCombat, combat) || !ReferenceEquals(_selectedEntity, entity) || _sorting != group;
        _active = active;
        SelectedCombat = combat; _selectedEntity = entity; _sorting = group;
        this.RaisePropertyChanged(nameof(SelectedSortName));
        if (changed) { Cancel(); _built = null; _rows.Clear(); DataToView.Clear(); Totals.Clear(); NotifyResults(); }
        if (_active) Update(); else { Cancel(); IsLoading = false; }
    }
    public override void UpdateData(Combat combat) { SelectedCombat = combat; Update(); }
    internal override void UpdateParticipant() => Update();
    public override void Reset()
    {
        Cancel(); SelectedCombat = null; _selectedEntity = null; _built = null; _building = null;
        _rows.Clear(); DataToView.Clear(); Totals.Clear(); IsLoading = false; NotifyResults();
    }
    private void Cancel() { Interlocked.Increment(ref _version); _pending?.Cancel(); }
    internal override void Update()
    {
        if (!_active || SelectedCombat == null || _selectedEntity == null) return;
        var key = (SelectedCombat, _selectedEntity, SelectedCombat.AllLogs.Count, SelectedCombat.DurationSeconds, _sorting);
        if (_built == key || (_pending is { IsCancellationRequested: false } && _building == key)) return;
        LastUpdate = Rebuild(key);
    }
    private async Task Rebuild((Combat Combat, Entity Entity, int Count, int Duration, SortingOption Group) key)
    {
        using var timing = LogLoadDiagnostics.Measure("Selected combat asynchronous detail updates");
        Cancel(); var version = _version;
        using var pending = new CancellationTokenSource(); _pending = pending; _building = key;
        IsLoading = true;
        try
        {
            var rows = await Task.Run(() => BuildRows(key.Combat, key.Entity, _type, key.Group, pending.Token), pending.Token);
            if (version != _version) return;
            // Keep bound rows during live updates; icon readiness never gates numbers.
            var previous = _rows.ToDictionary(row => row.SortItem, StringComparer.Ordinal);
            _rows.Clear();
            foreach (var row in rows)
            {
                if (previous.TryGetValue(row.SortItem, out var existing)) { existing.UpdateValues(row); _rows.Add(existing); }
                else _rows.Add(row);
            }
            var count = rows.Sum(r => r.Count);
            Totals.Clear();
            if (rows.Count > 0) Totals.Add(new CombatInfoInstance { SortItem = "Total", Total = rows.Sum(r => r.Total), SumTotal = rows.Sum(r => r.Total),
                RateDouble = rows.Sum(r => r.RateDouble), Count = count,
                Average = count == 0 ? 0 : rows.Sum(r => r.Total) / count,
                CritPercent = count == 0 ? 0 : rows.Sum(r => r.CritPercent * r.Count) / count,
                Max = rows.Max(r => r.Max), MaxCrit = rows.Max(r => r.MaxCrit),
                Type = rows.Select(r => r.Type).Distinct().Count() == 1 ? rows[0].Type : "Mixed" });
            _built = key; ApplyFilter();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Logging.LogError("Failed to update overview: " + error.Message); }
        finally { if (ReferenceEquals(_pending, pending)) { _pending = null; _building = null; IsLoading = false; } }
    }
    private void ApplyFilter()
    {
        var desired = _rows.Where(r => r.SortItem.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        var included = desired.ToHashSet();
        for (var i = DataToView.Count - 1; i >= 0; i--) if (!included.Contains(DataToView[i])) DataToView.RemoveAt(i);
        for (var i = 0; i < desired.Length; i++)
        {
            var previous = DataToView.IndexOf(desired[i]);
            if (previous == i) continue;
            if (previous >= 0) DataToView.RemoveAt(previous);
            DataToView.Insert(i, desired[i]);
        }
        NotifyResults();
    }
    private void NotifyResults()
    {
        this.RaisePropertyChanged(nameof(Summary)); this.RaisePropertyChanged(nameof(ResultCount));
        this.RaisePropertyChanged(nameof(Overall)); this.RaisePropertyChanged(nameof(HasTotals));
        this.RaisePropertyChanged(nameof(IsEmpty)); this.RaisePropertyChanged(nameof(EmptyMessage));
    }

    internal static List<CombatInfoInstance> BuildRows(Combat combat, Entity entity, OverviewDataType type, SortingOption group, CancellationToken token)
    {
        IEnumerable<ParsedLogEntry> logs = type switch
        {
            OverviewDataType.Damage => combat.OutgoingDamageLogs.Where(p => p.Key.LogId == entity.LogId).SelectMany(p => p.Value),
            OverviewDataType.Healing => combat.OutgoingHealingLogs.Where(p => p.Key.LogId == entity.LogId).SelectMany(p => p.Value)
                .Concat(combat.ShieldingProvidedLogs.Where(p => p.Key.LogId == entity.LogId).SelectMany(p => p.Value)),
            OverviewDataType.DamageTaken => combat.IncomingDamageLogs.Where(p => p.Key.LogId == entity.LogId).SelectMany(p => p.Value),
            OverviewDataType.HealingReceived => combat.IncomingHealingLogs.Where(p => p.Key.LogId == entity.LogId).SelectMany(p => p.Value),
            _ => combat.GetLogsInvolvingEntity(entity).Where(l => l.Source.LogId == entity.LogId && l.Threat != 0)
        };
        var rows = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        var sum = 0d; var index = 0;
        foreach (var log in logs)
        {
            if ((index++ & 255) == 0) token.ThrowIfCancellationRequested();
            var name = group switch { SortingOption.BySource => log.Source.Name, SortingOption.ByTarget => log.Target.Name, _ => log.Ability };
            if (string.IsNullOrEmpty(name)) name = type == OverviewDataType.Threat ? "Taunt" : "Unknown";
            if (!rows.TryGetValue(name, out var row)) rows[name] = row = new Accumulator(log);
            var value = type == OverviewDataType.Threat ? log.Threat : log.Value.EffectiveDblValue;
            row.Total += value; row.Count++;
            if (log.Value.WasCrit) { row.CritCount++; row.MaxCrit = Math.Max(row.MaxCrit, value); }
            else row.Max = Math.Max(row.Max, value);
            if (log.Value.ValueType is DamageType.intern or DamageType.energy or DamageType.kinetic or DamageType.heal or DamageType.elemental)
                row.Types.Add(log.Value.ValueType);
            sum += type == OverviewDataType.Threat ? Math.Max(0, value) : value;
        }
        return rows.Select(p => new CombatInfoInstance { SortItem = p.Key, SumTotal = sum, Total = p.Value.Total,
            RateDouble = combat.DurationSeconds > 0 ? p.Value.Total / combat.DurationSeconds : 0,
            Average = p.Value.Total / p.Value.Count, Count = p.Value.Count,
            Max = p.Value.Max, MaxCrit = p.Value.MaxCrit, CritPercent = p.Value.CritCount * 100d / p.Value.Count,
            Type = p.Value.Types.Count > 1 ? "Mixed" : p.Value.Types.FirstOrDefault() switch
            { DamageType.intern => "Internal", DamageType.energy => "Energy", DamageType.kinetic => "Kinetic", DamageType.heal => "Healing", DamageType.elemental => "Elemental", _ => "—" },
            IconLog = p.Value.First, IconGrouping = group }).OrderByDescending(r => r.Total).ThenBy(r => r.SortItem).ToList();
    }
    private sealed class Accumulator(ParsedLogEntry first)
    {
        internal readonly ParsedLogEntry First = first;
        internal readonly HashSet<DamageType> Types = new();
        internal double Total, Max, MaxCrit;
        internal int Count, CritCount;
    }
}
