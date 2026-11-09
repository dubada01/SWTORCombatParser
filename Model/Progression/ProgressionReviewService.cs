using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatCaching;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.Phases;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.Phases;

namespace SWTORCombatParser.Model.Progression;

public sealed class ProgressionReviewService : IDisposable
{
    private readonly CombatMonitorViewModel _monitor;
    private readonly ProgressionHistoryStore _store = new();
    private readonly SemaphoreSlim _reader = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, ProgressionPullSummary> _current = new(StringComparer.Ordinal);
    private ObservableCollection<EncounterCombat>? _observed;
    private Dictionary<string, Source> _sources = new(StringComparer.Ordinal);
    private ProgressionHistorySnapshot _saved = new(-1, Array.Empty<ProgressionSavedRaid>(), false);
    private PhaseCatalog _phases = new(Array.Empty<Phase>());
    private CancellationTokenSource? _request;
    private string? _priorityPull;
    private string? _priorityRaid;
    private bool _active;
    private bool _disposed;

    internal event Action? Changed;
    internal IReadOnlyList<ProgressionRaidChoice> Raids { get; private set; } = Array.Empty<ProgressionRaidChoice>();
    internal IReadOnlyCollection<ProgressionPullSummary> CurrentPulls => _current.Values;
    internal IReadOnlyList<ProgressionSavedRaid> SavedRaids => _saved.Raids;
    internal bool IsIndexing { get; private set; }
    internal string? Warning { get; private set; }
    internal bool HasHistoryErrors => _saved.HasReadErrors;

    public ProgressionReviewService(CombatMonitorViewModel monitor)
    {
        _monitor = monitor;
        _monitor.CompletedLivePull += OnLivePull;
    }

    internal string? Activate(PastCombat? focus)
    {
        if (!_active)
        {
            _active = true;
            _monitor.PropertyChanged += OnMonitorChanged;
            EncounterMonitor.EncounterUpdated += OnEncounterUpdated;
            PhaseListViewModel.PhasesUpdated += QueueRefresh;
        }

        var preferred = focus?.ParentEncounter ?? _monitor.PastEncounters.FirstOrDefault(encounter =>
            encounter.EncounterCombats.Any(pull => pull.IsBossCombat && !pull.IsCurrentCombat));
        var raid = preferred == null ? null : CaptureRaid(preferred);
        var focusId = raid != null && focus != null ? PullId(raid, focus.CombatStartTime) : null;
        Prioritize(focusId, raid?.Id);
        Refresh(force: _request == null && _sources.Count == 0);
        return focusId;
    }

    internal void Prioritize(string? pull, string? raid)
    {
        Volatile.Write(ref _priorityPull, pull);
        Volatile.Write(ref _priorityRaid, raid);
    }

    internal void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _monitor.PropertyChanged -= OnMonitorChanged;
        EncounterMonitor.EncounterUpdated -= OnEncounterUpdated;
        PhaseListViewModel.PhasesUpdated -= QueueRefresh;
        if (_observed != null) _observed.CollectionChanged -= OnCollectionChanged;
        _observed = null;
        _request?.Cancel();
        _request = null;
        _current.Clear();
        _sources.Clear();
        Raids = Array.Empty<ProgressionRaidChoice>();
        IsIndexing = false;
    }

    internal ProgressionPullSummary WithCurrentPhases(ProgressionPullSummary pull) => pull.State == ProgressionDataState.Available &&
        pull.PhaseRevision != _phases.For(pull.Metadata).Revision ? pull.WithoutPhases() : pull;

    internal PastCombat? FindLoadedPull(string id) => _sources.TryGetValue(id, out var source) && source.Row.TryGetTarget(out var row)
        ? row : null;

    internal async Task AssignTeamAsync(ProgressionRaidChoice raid, string? team)
    {
        try
        {
            var snapshot = await Task.Run(() =>
            {
                _store.AssignTeam(raid, team);
                return _store.Read();
            });
            Warning = null;
            SetSaved(snapshot);
        }
        catch (Exception error) when (ProgressionHistoryStore.IsFileError(error))
        {
            Warning = "Could not save the team assignment.";
            Debug.WriteLine(error);
        }
        if (!_disposed) Changed?.Invoke();
    }

    private void OnEncounterUpdated(EncounterCombat _) => QueueRefresh();
    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CombatMonitorViewModel.PastEncounters)) QueueRefresh();
    }
    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => QueueRefresh();
    private void QueueRefresh() => Dispatcher.UIThread.Post(() => { if (_active) Refresh(); }, DispatcherPriority.Background);

    private void Refresh(bool force = false)
    {
        if (_disposed) return;
        if (!ReferenceEquals(_observed, _monitor.PastEncounters))
        {
            if (_observed != null) _observed.CollectionChanged -= OnCollectionChanged;
            _observed = _monitor.PastEncounters;
            _observed.CollectionChanged += OnCollectionChanged;
        }
        var sources = new Dictionary<string, Source>(StringComparer.Ordinal);
        var raids = new List<ProgressionRaidChoice>();
        foreach (var encounter in _observed)
        {
            var raid = CaptureRaid(encounter);
            if (raid == null) continue;
            var pulls = encounter.EncounterCombats.Where(pull => pull.IsBossCombat && !pull.IsCurrentCombat).ToArray();
            if (pulls.Length == 0) continue;
            raids.Add(raid);
            foreach (var row in pulls)
            {
                var source = CaptureSource(raid, row);
                sources[source.Metadata.Id] = source;
            }
        }

        var definitions = PhaseManager.CaptureDefinitions();
        var phasesChanged = !ReferenceEquals(definitions, _phases.Definitions);
        if (phasesChanged) _phases = new PhaseCatalog(definitions);
        if (!force && !phasesChanged && sources.Count == _sources.Count && sources.Values.All(source =>
            _sources.TryGetValue(source.Metadata.Id, out var old) && source.SameInput(old))) return;

        _request?.Cancel();
        _sources = sources;
        Raids = raids.OrderByDescending(raid => raid.Start).ToArray();
        foreach (var id in _current.Keys.Where(id => !sources.ContainsKey(id)).ToArray()) _current.Remove(id);
        foreach (var source in sources.Values)
        {
            if (!_current.TryGetValue(source.Metadata.Id, out var old) || old.Metadata != source.Metadata ||
                old.State != ProgressionDataState.Available || old.PhaseRevision != _phases.For(source.Metadata).Revision)
                _current[source.Metadata.Id] = ProgressionPullSummary.Placeholder(source.Metadata, ProgressionDataState.Pending);
        }
        var pending = sources.Values.Where(source => _current[source.Metadata.Id].State == ProgressionDataState.Pending)
            .OrderBy(source => source.Metadata.Start).ToList();
        var request = new CancellationTokenSource();
        _request = request;
        IsIndexing = pending.Count != 0 || _saved.Revision < 0;
        Changed?.Invoke();
        _ = IndexAsync(pending, _phases, request);
    }

    private async Task IndexAsync(List<Source> pending, PhaseCatalog phases, CancellationTokenSource request)
    {
        var token = request.Token;
        try
        {
            await Task.Run(async () =>
            {
                var snapshot = _store.Read();
                var saved = snapshot.Raids.SelectMany(raid => raid.Pulls)
                    .ToDictionary(pull => pull.Metadata.Id, StringComparer.Ordinal);
                await PublishAsync(request, snapshot, Array.Empty<ProgressionPullSummary>(), null);
                var batch = new List<(Source Source, ProgressionPullSummary Summary, bool Save)>();
                var clock = Stopwatch.StartNew();
                try
                {
                    var first = true;
                    while (pending.Count > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        var source = TakeNext(pending);
                        var definition = phases.For(source.Metadata);
                        saved.TryGetValue(source.Metadata.Id, out var previous);
                        var reusable = previous?.State == ProgressionDataState.Available && previous.Metadata == source.Metadata &&
                            previous.PhaseRevision == definition.Revision;
                        var summary = reusable ? previous! : await ReadPullAsync(source, definition, token).ConfigureAwait(false);
                        if (summary.State == ProgressionDataState.Unavailable && previous?.State == ProgressionDataState.Available &&
                            previous.Metadata == source.Metadata) summary = previous.WithoutPhases();
                        batch.Add((source, summary, !reusable && (summary.State != ProgressionDataState.Available ||
                            summary.PhaseRevision == definition.Revision)));
                        if (first || source.Metadata.Id == Volatile.Read(ref _priorityPull) || clock.ElapsedMilliseconds >= 180)
                        {
                            await PublishBatchAsync(batch, request);
                            first = false;
                            clock.Restart();
                        }
                    }
                }
                finally
                {
                    // Save completed detached work even if the review was closed or the log changed.
                    if (batch.Count > 0) await PublishBatchAsync(batch, request);
                }
            }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (ReferenceEquals(_request, request)) Warning = "Could not finish indexing these pulls.";
            Debug.WriteLine(error);
        }
        finally
        {
            if (ReferenceEquals(_request, request))
            {
                _request = null;
                IsIndexing = false;
                foreach (var summary in _current.Values.Where(pull => pull.State == ProgressionDataState.Pending).ToArray())
                    _current[summary.Metadata.Id] = ProgressionPullSummary.Placeholder(summary.Metadata, ProgressionDataState.Unavailable);
                Changed?.Invoke();
            }
            request.Dispose();
        }
    }

    private Source TakeNext(List<Source> pending)
    {
        var pull = Volatile.Read(ref _priorityPull);
        var raid = Volatile.Read(ref _priorityRaid);
        var index = pull == null ? -1 : pending.FindLastIndex(source => source.Metadata.Id == pull);
        if (index < 0 && raid != null) index = pending.FindLastIndex(source => source.Raid.Id == raid);
        if (index < 0) index = pending.Count - 1;
        var next = pending[index];
        pending.RemoveAt(index);
        return next;
    }

    private async Task<ProgressionPullSummary> ReadPullAsync(Source source, PhaseDefinition definition, CancellationToken token)
    {
        await _reader.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var timing = Utilities.LogLoadDiagnostics.Measure("Index progression pull");
            var logs = source.CachePath != null ? CombatDiskCacheManager.LoadDetachedLogs(source.CachePath, source.Owner, token)
                : source.Resident != null && source.Resident.TryGetTarget(out var combat) ? combat.AllLogs
                    .OrderBy(item => item.Key).Select(item => item.Value).ToList() : new List<ParsedLogEntry>();
            return ProgressionSummaryBuilder.Build(source.Metadata, logs, definition.Phases, definition.Revision, token);
        }
        catch (Exception error) when (ProgressionHistoryStore.IsFileError(error) || error is NotSupportedException)
        {
            Debug.WriteLine(error);
            return ProgressionPullSummary.Placeholder(source.Metadata, ProgressionDataState.Unavailable);
        }
        finally { _reader.Release(); }
    }

    private async Task PublishBatchAsync(List<(Source Source, ProgressionPullSummary Summary, bool Save)> batch,
        CancellationTokenSource request)
    {
        var completed = batch.ToArray();
        batch.Clear();
        string? warning = null;
        foreach (var group in completed.Where(item => item.Save && !item.Source.Raid.IsReplay).GroupBy(item => item.Source.Raid))
        {
            try { _store.Upsert(group.Key, group.Select(item => item.Summary)); }
            catch (Exception error) when (ProgressionHistoryStore.IsFileError(error))
            {
                warning = "Some pulls could not be saved to history.";
                Debug.WriteLine(error);
            }
        }
        await PublishAsync(request, _store.Read(), completed.Select(item => item.Summary).ToArray(), warning);
    }

    private async Task PublishAsync(CancellationTokenSource request, ProgressionHistorySnapshot snapshot,
        IReadOnlyList<ProgressionPullSummary> completed, string? warning) => await Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (!_active || _disposed || !ReferenceEquals(_request, request)) return;
        SetSaved(snapshot);
        foreach (var summary in completed) _current[summary.Metadata.Id] = summary;
        Warning = warning ?? Warning;
        Changed?.Invoke();
    }, DispatcherPriority.Background);

    private void SetSaved(ProgressionHistorySnapshot snapshot)
    {
        if (snapshot.Revision >= _saved.Revision) _saved = snapshot;
    }

    private void OnLivePull(PastCombat row) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed || row.ParentEncounter == null || CaptureRaid(row.ParentEncounter) is not { } raid) return;
        if (_active) Refresh();
        var source = CaptureSource(raid, row);
        _ = SaveLivePullAsync(source, new PhaseCatalog(PhaseManager.CaptureDefinitions()).For(source.Metadata), _lifetime.Token);
    }, DispatcherPriority.Background);

    private async Task SaveLivePullAsync(Source source, PhaseDefinition definition, CancellationToken token)
    {
        try
        {
            var snapshot = await Task.Run(async () =>
            {
                var summary = await ReadPullAsync(source, definition, token).ConfigureAwait(false);
                _store.Upsert(source.Raid, new[] { summary });
                return _store.Read();
            }, token);
            if (_disposed) return;
            SetSaved(snapshot);
            if (_active) Changed?.Invoke();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_disposed) return;
            Warning = "The latest live pull could not be saved to history.";
            Debug.WriteLine(error);
            if (_active) Changed?.Invoke();
        }
    }

    private static ProgressionRaidChoice? CaptureRaid(EncounterCombat encounter)
    {
        var first = encounter.EncounterCombats.Where(pull => !pull.IsCurrentCombat).MinBy(pull => pull.CombatStartTime);
        if (first == null || first.CombatStartTime == default || string.IsNullOrWhiteSpace(encounter.Info?.Name)) return null;
        var log = System.IO.Path.GetFileName(first.SourceLogName ?? "").ToUpperInvariant();
        var id = Hash(FormattableString.Invariant($"{log}\n{encounter.Info.LogId}\n{encounter.Info.Name}\n{first.CombatStartTime.Ticks}"));
        return new ProgressionRaidChoice(id, encounter.Info.Name, first.CombatStartTime, first.IsReplay);
    }

    private static Source CaptureSource(ProgressionRaidChoice raid, PastCombat row)
    {
        var start = row.CombatStartTime;
        var info = row.EncounterInfo;
        var groupSize = Known(row.BossDetails.GroupSize, Known(info?.NumberOfPlayer, "Unknown size"))
            .Replace("Player", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (int.TryParse(groupSize, out var size)) groupSize = $"{size}-player";
        var cohort = new ProgressionCohort(Known(info?.Name, raid.Operation),
            Known(row.BossDetails.Difficulty, Known(info?.Difficutly, "Unknown mode")), groupSize);
        var metadata = new ProgressionPullMetadata(PullId(raid, start), raid.Id, cohort, Known(row.BossDetails.Boss, row.CombatLabel),
            start, row.CombatEndTime >= start ? row.CombatEndTime : start.AddSeconds(row.CombatDurationSeconds), row.WasBossKilled);
        var path = string.IsNullOrWhiteSpace(row.CombatCachePath) ? null : row.CombatCachePath;
        return new Source(raid, metadata, path, row.CacheOwner, new WeakReference<PastCombat>(row),
            path == null && row.Combat is { } combat ? new WeakReference<Combat>(combat) : null);
    }

    private static string PullId(ProgressionRaidChoice raid, DateTime start) => raid.Id + "-" + start.Ticks.ToString(CultureInfo.InvariantCulture);
    private static string Known(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    private static string Hash(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Deactivate();
        _monitor.CompletedLivePull -= OnLivePull;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Changed = null;
    }

    private sealed record Source(ProgressionRaidChoice Raid, ProgressionPullMetadata Metadata, string? CachePath, Guid Owner,
        WeakReference<PastCombat> Row, WeakReference<Combat>? Resident)
    {
        public bool SameInput(Source old) => Metadata == old.Metadata && CachePath == old.CachePath && Owner == old.Owner;
    }

    private sealed record PhaseDefinition(string Revision, IReadOnlyList<Phase> Phases);

    private sealed class PhaseCatalog(IEnumerable<Phase> definitions)
    {
        private static readonly PhaseDefinition Empty = new("none", Array.Empty<Phase>());
        private readonly IReadOnlyDictionary<(string Operation, string Boss), PhaseDefinition> _byBoss = definitions
            .Where(phase => phase.PhaseSource?.Contains('|') == true && phase.SourcePt2 != null)
            .GroupBy(phase => (phase.PhaseSource.Split('|', 2)[0], phase.SourcePt2))
            .ToDictionary(group => group.Key, group => new PhaseDefinition(Hash(JsonSerializer.Serialize(group)), group.ToArray()));

        public IEnumerable<Phase> Definitions { get; } = definitions;
        public PhaseDefinition For(ProgressionPullMetadata pull) => _byBoss.GetValueOrDefault((pull.Cohort.Operation, pull.Boss)) ?? Empty;
    }
}
