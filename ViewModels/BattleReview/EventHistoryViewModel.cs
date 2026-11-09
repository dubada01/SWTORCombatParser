using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.LogReview;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.BattleReview;

public class EventHistoryViewModel : ReactiveObject
{
    private Combat? _combat;
    private Combat? _indexedCombat;
    private int _indexedCount = -1;
    private DateTime _indexedStart;
    private Task<ReviewIndex>? _indexTask;
    private ReviewIndex? _displayedIndex;
    private Combat? _displayedCombat;
    private readonly Dictionary<ParsedLogEntry, DisplayableLogEntry> _rowCache = new();
    private CancellationTokenSource? _pending;
    private int _version;
    private string _filter = "";
    private bool _regex;
    private List<Entity> _entities = new();
    private DisplayType _type;
    private ParticipantRole? _participantRole;
    private ParsedLogEntry[] _displayed = Array.Empty<ParsedLogEntry>();
    private IReadOnlyList<DisplayableLogEntry> _logs = Array.Empty<DisplayableLogEntry>();
    private int _selectedIndex = -1;
    private bool _publishing;
    private bool _isLoading;
    private string _searchError = "";
    private int _totalCount;

    public event Action<double, List<EntityInfo>> LogPositionChanged = delegate { };
    public IReadOnlyList<DisplayableLogEntry> LogsToDisplay { get => _logs; private set => this.RaiseAndSetIfChanged(ref _logs, value); }
    public bool HasFocus { get; set; }
    public bool DeathReview { get; private set; }
    public bool DisplayOffensiveBuffs { get; private set; }
    public bool DisplayDefensiveBuffs => !DisplayOffensiveBuffs;
    public bool IsLoading { get => _isLoading; private set { this.RaiseAndSetIfChanged(ref _isLoading, value); NotifyStatus(); } }
    public string SearchError { get => _searchError; private set { this.RaiseAndSetIfChanged(ref _searchError, value); this.RaisePropertyChanged(nameof(HasSearchError)); NotifyStatus(); } }
    public bool HasSearchError => SearchError.Length > 0;
    public bool IsEmpty => !IsLoading && LogsToDisplay.Count == 0;
    public string EmptyMessage => _combat == null ? "Select a combat to review" : _totalCount == 0 ? "This combat contains no logged events" : "No events match these filters";
    public string ResultSummary => IsLoading ? "Searching…" : HasSearchError ? $"Search not applied · {LogsToDisplay.Count:N0} events shown" : $"{LogsToDisplay.Count:N0} / {_totalCount:N0} events";
    public int MaterializedRowCount => _rowCache.Count;
    public DisplayableLogEntry? SelectedEntry => _selectedIndex >= 0 && _selectedIndex < LogsToDisplay.Count ? LogsToDisplay[_selectedIndex] : null;
    public bool HasSelection => SelectedEntry != null;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex == value) return;
            this.RaiseAndSetIfChanged(ref _selectedIndex, value);
            this.RaisePropertyChanged(nameof(SelectedEntry));
            this.RaisePropertyChanged(nameof(HasSelection));
            if (!_publishing && HasFocus && DeathReview && SelectedEntry is { } row)
                LogPositionChanged(row.ElapsedSeconds, GetInfosNearLog(row.ElapsedSeconds));
        }
    }

    public Task<DateTime> SelectCombat(Combat combatSeleted, bool inverted = false)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return Dispatcher.UIThread.Invoke(() => SelectCombat(combatSeleted, inverted));
        _combat = combatSeleted;
        return UpdateLogs(inverted);
    }
    public void SetViewableEntities(List<Entity> entitiesToshow)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Invoke(() => SetViewableEntities(entitiesToshow)); return; }
        _entities = entitiesToshow.ToList(); Invalidate();
    }
    public void SetDisplayType(DisplayType type)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Invoke(() => SetDisplayType(type)); return; }
        _type = type; Invalidate();
        if (type == DisplayType.Abilities) return;
        DisplayOffensiveBuffs = type is DisplayType.All or DisplayType.Damage or DisplayType.Healing;
        this.RaisePropertyChanged(nameof(DisplayOffensiveBuffs)); this.RaisePropertyChanged(nameof(DisplayDefensiveBuffs));
    }
    public Task SetFilter(string logFilter) { SetFilterText(logFilter); return UpdateLogs(DeathReview); }
    public void SetFilterText(string text)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Invoke(() => SetFilterText(text)); return; }
        _filter = text ?? ""; Invalidate();
    }
    public void SetRegexMode(bool enabled)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Invoke(() => SetRegexMode(enabled)); return; }
        _regex = enabled; Invalidate();
    }
    private void Invalidate() { Interlocked.Increment(ref _version); _pending?.Cancel(); }
    public void SetParticipantRole(ParticipantRole role)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Invoke(() => SetParticipantRole(role)); return; }
        _participantRole = role; Invalidate();
    }
    internal void ClearCombat()
    {
        Invalidate();
        _combat = _indexedCombat = _displayedCombat = null;
        _indexTask = null; _displayedIndex = null; _indexedCount = -1; _totalCount = 0;
        _displayed = Array.Empty<ParsedLogEntry>(); _rowCache.Clear();
        LogsToDisplay = Array.Empty<DisplayableLogEntry>(); SelectedIndex = -1;
        SearchError = ""; IsLoading = false;
    }
    private void NotifyStatus() { this.RaisePropertyChanged(nameof(ResultSummary)); this.RaisePropertyChanged(nameof(IsEmpty)); this.RaisePropertyChanged(nameof(EmptyMessage)); }

    public async Task<DateTime> UpdateLogs(bool isDethReview = false)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return await Dispatcher.UIThread.Invoke(() => UpdateLogs(isDethReview));
        if (_combat == null) return DateTime.MinValue;
        using var timing = LogLoadDiagnostics.Measure("Selected combat asynchronous log review");
        var version = Interlocked.Increment(ref _version);
        _pending?.Cancel();
        var pending = new CancellationTokenSource(); _pending = pending;
        var token = pending.Token;
        var combat = _combat;
        var filter = _filter; var regex = _regex; var type = _type; var role = _participantRole;
        var ids = _entities.Select(e => e.LogId).ToHashSet();
        var showAll = _entities.Any(e => e.Name == "All");
        await Dispatcher.UIThread.InvokeAsync(() => { if (version == _version) { IsLoading = true; SearchError = ""; } });
        try
        {
            var query = new LogSearchQuery(filter, regex);
            // Reuse the sorted snapshot when changing filters. A growing live combat gets a new snapshot.
            if (!ReferenceEquals(_indexedCombat, combat) || _indexedCount != combat.AllLogs.Count || _indexedStart != combat.StartTime)
            {
                _indexedCombat = combat; _indexedCount = combat.AllLogs.Count; _indexedStart = combat.StartTime;
                var start = combat.StartTime;
                _indexTask = Task.Run(() => new ReviewIndex(combat.AllLogs.ToArray().OrderBy(p => p.Value.TimeStamp).ThenBy(p => p.Key).Select(p => p.Value).ToArray(), start));
            }
            var index = await _indexTask!.WaitAsync(token);
            var result = await Task.Run(() =>
            {
                using var filterTiming = LogLoadDiagnostics.Measure("Log review filter worker");
                var watch = Stopwatch.StartNew();
                var matches = new List<ParsedLogEntry>();
                for (var i = 0; i < index.Logs.Length; i++)
                {
                    if ((i & 255) == 0)
                    {
                        token.ThrowIfCancellationRequested();
                        if (regex && watch.ElapsedMilliseconds > 1500) throw new RegexMatchTimeoutException();
                    }
                    var log = index.Logs[i];
                    if (MatchesType(log, type, ids, showAll, role) && query.Matches(log, (log.TimeStamp - index.Start).TotalSeconds)) matches.Add(log);
                }
                var death = DateTime.MinValue;
                if (isDethReview)
                {
                    var firstDeath = matches.FirstOrDefault(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && l.Target.IsCharacter);
                    if (firstDeath != null) death = firstDeath.TimeStamp.AddSeconds(-15);
                    matches.RemoveAll(l => l.TimeStamp <= death);
                }
                return (Logs: matches.ToArray(), Death: death);
            }, token);
            token.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                using var publishTiming = LogLoadDiagnostics.Measure("Log review publish UI");
                if (version != _version) return;
                var selected = SelectedEntry?.LogEntry;
                var sameIndex = ReferenceEquals(index, _displayedIndex);
                var sameCombat = ReferenceEquals(combat, _displayedCombat) && index.Start == _displayedIndex?.Start;
                _totalCount = index.Logs.Length; DeathReview = isDethReview;
                // No result change means no ItemsSource reset, selection change, or scroll jump.
                if (sameIndex && _displayed.SequenceEqual(result.Logs)) { NotifyStatus(); return; }
                if (!sameIndex && sameCombat && _displayed.Length > 0 && LogsToDisplay is LogReviewRows existing && result.Logs.Length >= _displayed.Length &&
                    result.Logs.AsSpan(0, _displayed.Length).SequenceEqual(_displayed))
                {
                    existing.Append(result.Logs);
                    _displayed = result.Logs; _displayedIndex = index;
                    NotifyStatus(); return;
                }
                if (!sameCombat) _rowCache.Clear();
                _displayedCombat = combat;
                _displayedIndex = index; _displayed = result.Logs;
                _publishing = true;
                try
                {
                    LogsToDisplay = new LogReviewRows(result.Logs, log => GetRow(log, index.Start));
                    SelectedIndex = selected == null ? -1 : Array.IndexOf(result.Logs, selected);
                    this.RaisePropertyChanged(nameof(SelectedEntry)); this.RaisePropertyChanged(nameof(HasSelection));
                }
                finally { _publishing = false; }
                NotifyStatus();
            });
            return result.Death;
        }
        catch (OperationCanceledException) { return DateTime.MinValue; }
        catch (Exception error) when (error is FormatException or RegexMatchTimeoutException)
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                if (version != _version) return;
                if (!ReferenceEquals(_displayedCombat, combat))
                {
                    _rowCache.Clear(); _displayed = Array.Empty<ParsedLogEntry>(); _displayedIndex = null;
                    _displayedCombat = combat; _totalCount = combat.AllLogs.Count;
                    LogsToDisplay = Array.Empty<DisplayableLogEntry>(); SelectedIndex = -1;
                }
                SearchError = error is RegexMatchTimeoutException ? "Regex took too long. Narrow the expression or use plain text." : error.Message;
            });
            return DateTime.MinValue;
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (ReferenceEquals(_pending, pending)) { IsLoading = false; _pending = null; } });
            pending.Dispose();
        }
    }

    private DisplayableLogEntry GetRow(ParsedLogEntry log, DateTime start)
    {
        if (!_rowCache.TryGetValue(log, out var row)) _rowCache.Add(log, row = new DisplayableLogEntry(log, start));
        return row;
    }

    private static bool MatchesType(ParsedLogEntry log, DisplayType type, HashSet<long> ids, bool all, ParticipantRole? role)
    {
        var source = ids.Contains(log.Source.LogId); var target = ids.Contains(log.Target.LogId);
        var direction = role ?? (type switch { DisplayType.Damage or DisplayType.Healing => ParticipantRole.Source,
            DisplayType.DamageTaken or DisplayType.HealingReceived => ParticipantRole.Target, _ => ParticipantRole.Either });
        if (!all && !(direction switch { ParticipantRole.Source => source, ParticipantRole.Target => target, _ => source || target })) return false;
        return type switch
        {
            DisplayType.All => true,
            DisplayType.Damage or DisplayType.DamageTaken => log.Effect.EffectType == EffectType.Apply && log.Effect.EffectId == _7_0LogParsing._damageEffectId,
            DisplayType.Healing or DisplayType.HealingReceived => log.Effect.EffectType == EffectType.Apply && log.Effect.EffectId == _7_0LogParsing._healEffectId,
            DisplayType.Abilities => log.Effect.EffectId == _7_0LogParsing.AbilityActivateId,
            DisplayType.DeathRecap => IsDeathRecap(log, ids), _ => false
        };
    }
    private static bool IsDeathRecap(ParsedLogEntry log, HashSet<long> ids) =>
        log.Effect.EffectId != 836045448938502 && log.Effect.EffectId != _7_0LogParsing._healEffectId && log.Effect.EffectType != EffectType.Remove &&
        !((ids.Contains(log.Source.LogId) || log.Source.IsCharacter) && log.Effect.EffectId == _7_0LogParsing._damageEffectId) &&
        !(log.Source.IsCharacter && !log.Target.IsCharacter && log.Effect.EffectType == EffectType.Apply) &&
        !(log.Source.IsCharacter && log.Target.IsCharacter && log.Effect.EffectId == _7_0LogParsing.AbilityActivateId);

    internal List<EntityInfo> Seek(double seconds)
    {
        if (!Dispatcher.UIThread.CheckAccess()) return Dispatcher.UIThread.Invoke(() => Seek(seconds));
        if (_displayed.Length == 0 || _displayedIndex == null) return new();
        SelectedIndex = Nearest(_displayed, _displayedIndex.Start.AddSeconds(seconds));
        return GetInfosNearLog(seconds);
    }
    private List<EntityInfo> GetInfosNearLog(double seconds)
    {
        if (_displayedIndex == null) return new();
        var time = _displayedIndex.Start.AddSeconds(seconds);
        var infos = new List<EntityInfo>();
        foreach (var pair in _displayedIndex.EntityLogs)
        {
            var log = pair.Value[Nearest(pair.Value, time)];
            infos.Add(log.Source.LogId == pair.Key ? log.SourceInfo : log.TargetInfo);
        }
        return infos;
    }
    private static int Nearest(ParsedLogEntry[] logs, DateTime time)
    {
        var low = 0; var high = logs.Length;
        while (low < high) { var mid = low + (high - low) / 2; if (logs[mid].TimeStamp < time) low = mid + 1; else high = mid; }
        if (low == logs.Length) return low - 1;
        return low > 0 && time - logs[low - 1].TimeStamp <= logs[low].TimeStamp - time ? low - 1 : low;
    }
    private sealed class ReviewIndex
    {
        public readonly ParsedLogEntry[] Logs;
        public readonly DateTime Start;
        public Dictionary<long, ParsedLogEntry[]> EntityLogs { get; }
        public ReviewIndex(ParsedLogEntry[] logs, DateTime start) { Logs = logs; Start = start; EntityLogs = BuildEntityLogs(); }
        private Dictionary<long, ParsedLogEntry[]> BuildEntityLogs()
        {
            var lists = new Dictionary<long, List<ParsedLogEntry>>();
            foreach (var log in Logs)
            {
                if (!lists.TryGetValue(log.Source.LogId, out var source)) lists.Add(log.Source.LogId, source = new());
                source.Add(log);
                if (log.Target.LogId == log.Source.LogId) continue;
                if (!lists.TryGetValue(log.Target.LogId, out var target)) lists.Add(log.Target.LogId, target = new());
                target.Add(log);
            }
            return lists.ToDictionary(p => p.Key, p => p.Value.ToArray());
        }
    }
}
