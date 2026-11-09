using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.SessionReview;
using SWTORCombatParser.Model.CombatCaching;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.Model.SessionReview;

/// <summary>
/// Shared by the review window and compact overlay. Work is coalesced, cancellable and only
/// performed while a view holds a lease. Completed combats are read once, one at a time.
/// </summary>
public sealed class SessionReviewService : IDisposable
{
    private readonly CombatMonitorViewModel _monitor;
    private readonly DispatcherTimer _timer;
    private readonly SemaphoreSlim _worker = new(1, 1);
    private readonly Dictionary<PastCombat, SessionCombatSummary> _cache = new();
    private ObservableCollection<EncounterCombat>? _observedEncounters;
    private CancellationTokenSource? _request;
    private DateTime _lastBuild;
    private bool _dirty;
    private bool _pinned;
    private bool _disposed;
    private int _leases;
    private long _generation;

    public SessionReviewSnapshot? Snapshot { get; private set; }
    public EncounterCombat? SelectedEncounter { get; private set; }
    public IReadOnlyList<EncounterCombat> Encounters { get; private set; } = Array.Empty<EncounterCombat>();
    public string Status { get; private set; } = "Load a log or start parsing to review a session.";
    public event Action? Changed;

    public SessionReviewService(CombatMonitorViewModel monitor)
    {
        _monitor = monitor;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _timer.Tick += OnTick;
        EncounterMonitor.EncounterUpdated += OnEncounterUpdated;
        CombatLogStreamer.CombatUpdated += OnLiveCombat;
        _monitor.PropertyChanged += OnMonitorPropertyChanged;
        ObserveEncounters();
        var selected = EncounterMonitor.GetCurrentEncounter();
        SelectedEncounter = selected is not null && Encounters.Contains(selected) ? selected
            : Encounters.FirstOrDefault();
    }

    public IDisposable Acquire()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SessionReviewService));
        _leases++;
        Refresh();
        return new Lease(this);
    }

    public void SelectEncounter(EncounterCombat encounter)
    {
        _pinned = true;
        if (ReferenceEquals(SelectedEncounter, encounter)) return;
        SelectedEncounter = encounter;
        Snapshot = null;
        Refresh();
    }

    public void Refresh(EncounterCombat? encounter = null)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Refresh(encounter));
            return;
        }
        if (_disposed) return;
        if (encounter is not null) SelectedEncounter = encounter;
        _dirty = true;
        // Fence any result belonging to the previous encounter immediately.
        _generation++;
        _request?.Cancel();
        if (_leases > 0) _timer.Start();
    }

    private void OnEncounterUpdated(EncounterCombat encounter) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed) return;
        ObserveEncounters();
        if (!_pinned || SelectedEncounter is null) SelectedEncounter = encounter;
        if (ReferenceEquals(encounter, SelectedEncounter)) Refresh();
    });

    private void OnLiveCombat(CombatStatusUpdate combat) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed || !ReferenceEquals(SelectedEncounter, _monitor.CurrentEncounter)) return;
        // A stream update marks work pending without repeatedly cancelling the active build.
        _dirty = true;
        if (_leases > 0) _timer.Start();
    });

    private void OnMonitorPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CombatMonitorViewModel.PastEncounters))
            Dispatcher.UIThread.Post(() => { if (!_disposed) { ObserveEncounters(); Refresh(); } });
    }

    private void OnEncounterCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        Dispatcher.UIThread.Post(() => { if (!_disposed) { ObserveEncounters(); Refresh(); } });

    private void ObserveEncounters()
    {
        if (!ReferenceEquals(_observedEncounters, _monitor.PastEncounters))
        {
            if (_observedEncounters is not null) _observedEncounters.CollectionChanged -= OnEncounterCollectionChanged;
            _observedEncounters = _monitor.PastEncounters;
            _observedEncounters.CollectionChanged += OnEncounterCollectionChanged;
        }
        Encounters = _monitor.PastEncounters.ToArray();
        if (SelectedEncounter is not null && !Encounters.Contains(SelectedEncounter))
        {
            SelectedEncounter = Encounters.FirstOrDefault();
            _pinned = false;
            Snapshot = null;
        }
        SelectedEncounter ??= Encounters.FirstOrDefault();
        Changed?.Invoke();
    }

    private async void OnTick(object? sender, EventArgs args)
    {
        if (_disposed || _leases == 0) { _timer.Stop(); return; }
        if (!_dirty) { _timer.Stop(); return; }
        // Let an active build finish. Live events only queue a subsequent refresh; they must not starve it.
        if (_request is not null) return;
        if (DateTime.UtcNow - _lastBuild < TimeSpan.FromSeconds(2)) return;
        _dirty = false;
        _lastBuild = DateTime.UtcNow;
        var encounter = SelectedEncounter;
        if (encounter is null)
        {
            Snapshot = null;
            Status = "Load a log or start parsing to review a session.";
            Changed?.Invoke();
            return;
        }
        Source[] sources;
        string title;
        string details;
        try
        {
            sources = CaptureSources(encounter);
            title = encounter.Info?.Name ?? "Session";
            details = encounter.Info?.NamePlus ?? title;
        }
        catch (Exception error)
        {
            Status = "Unable to capture session: " + error.Message;
            Changed?.Invoke();
            return;
        }
        var generation = ++_generation;
        _request?.Cancel();
        var request = new CancellationTokenSource();
        _request = request;
        Status = Snapshot is null ? "Reading session performance…" : "Updating session performance…";
        Changed?.Invoke();
        try
        {
            var snapshot = await Task.Run(async () =>
            {
                await _worker.WaitAsync(request.Token).ConfigureAwait(false);
                try
                {
                    var results = new List<SessionCombatSummary>();
                    foreach (var source in sources)
                    {
                        request.Token.ThrowIfCancellationRequested();
                        if (!source.Ongoing && _cache.TryGetValue(source.Identity, out var cached))
                        {
                            results.Add(cached);
                            continue;
                        }
                        SessionCombatSummary summary;
                        try
                        {
                            summary = SessionReviewBuilder.Summarize(source.Name, source.Start, source.End,
                                source.Outcome, source.LoadLogs(), request.Token, source.IsBoss, source.Players);
                        }
                        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
                        {
                            summary = new SessionCombatSummary(source.Name, source.Start,
                                source.End > source.Start ? source.End : source.Start, source.Outcome,
                                new Dictionary<string, SessionPlayerMetrics>(), Array.Empty<string>(), false) { IsBoss = source.IsBoss };
                        }
                        results.Add(summary);
                        if (!source.Ongoing && summary.HasData) _cache[source.Identity] = summary;
                    }
                    // Bound cached summaries to the loaded log's current set of encounters.
                    var keep = sources.Select(s => s.Identity).ToHashSet();
                    foreach (var key in _cache.Keys.Where(k => !keep.Contains(k)).ToArray()) _cache.Remove(key);
                    return new SessionReviewSnapshot(title, details, results.OrderBy(c => c.Start).ToArray());
                }
                finally { _worker.Release(); }
            }, request.Token);
            if (_disposed || generation != _generation || request.IsCancellationRequested) return;
            Snapshot = snapshot;
            Status = snapshot.MissingCombats > 0
                ? $"{snapshot.MissingCombats} combat(s) have unavailable log data. Reload the source log to restore metrics."
                : snapshot.Combats.Any(c => c.Outcome == SessionCombatOutcome.InProgress)
                    ? "Live session · updates every 2 seconds · full combats, without phase filters"
                    : "Full combats, without phase filters · only players observed in the log are available";
            Changed?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            if (!_disposed && generation == _generation)
            {
                Status = "Unable to read session: " + error.Message;
                Changed?.Invoke();
            }
        }
        finally
        {
            if (ReferenceEquals(_request, request)) _request = null;
            request.Dispose();
        }
    }

    private static Source[] CaptureSources(EncounterCombat encounter)
    {
        return encounter.EncounterCombats.OrderBy(c => c.CombatStartTime).Select(past =>
        {
            // Capture references and metadata on the UI thread; never load/unload PastCombat in a worker.
            var combat = past.Combat;
            var path = past.CombatCachePath;
            var start = combat?.StartTime ?? past.CombatStartTime;
            // An unloaded fight gets its exact end from the cached raw logs, never from the
            // phase-dependent display duration or the parser-mutated shell collection.
            var end = combat?.EndTime ?? DateTime.MinValue;
            var outcome = past.IsCurrentCombat ? SessionCombatOutcome.InProgress
                : past.IsBossCombat ? (past.WasBossKilled ? SessionCombatOutcome.Kill : SessionCombatOutcome.Wipe)
                : encounter.Info?.IsPvpEncounter == true ? SessionCombatOutcome.PvP : SessionCombatOutcome.Combat;
            var name = past.IsBossCombat ? combat?.EncounterBossDifficultyParts.Item1 ?? past.CombatLabel
                : outcome == SessionCombatOutcome.PvP ? "PvP combat" : past.IsCurrentCombat ? "Combat in progress" : "Other combat";
            // Capture existing class attribution on the UI thread. Workers never consult mutable parser state.
            var players = new Dictionary<string, SessionPlayerInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in CombatLogStateBuilder.CurrentState.PlayerClassChangeInfo.Keys.ToArray())
            {
                if (string.IsNullOrEmpty(entity.Name)) continue;
                var characterClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(entity, start);
                players[entity.Name] = new SessionPlayerInfo(characterClass.Role, characterClass.Discipline ?? "");
            }
            if (combat is not null)
                foreach (var (entity, characterClass) in combat.CharacterClases.ToArray())
                    if (!string.IsNullOrEmpty(entity.Name) && characterClass is not null)
                        players[entity.Name] = new SessionPlayerInfo(characterClass.Role, characterClass.Discipline ?? "");
            IEnumerable<ParsedLogEntry> Load() => combat is not null ? combat.AllLogs.Values.ToArray()
                : !string.IsNullOrEmpty(path) ? CombatDiskCacheManager.LoadLogs(path) : Array.Empty<ParsedLogEntry>();
            return new Source(past, name, start, end, outcome, past.IsCurrentCombat, past.IsBossCombat, players, Load);
        }).ToArray();
    }

    private sealed record Source(PastCombat Identity, string Name, DateTime Start, DateTime End,
        SessionCombatOutcome Outcome, bool Ongoing, bool IsBoss, IReadOnlyDictionary<string, SessionPlayerInfo> Players,
        Func<IEnumerable<ParsedLogEntry>> LoadLogs);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _request?.Cancel();
        EncounterMonitor.EncounterUpdated -= OnEncounterUpdated;
        CombatLogStreamer.CombatUpdated -= OnLiveCombat;
        _monitor.PropertyChanged -= OnMonitorPropertyChanged;
        if (_observedEncounters is not null) _observedEncounters.CollectionChanged -= OnEncounterCollectionChanged;
        Changed = null;
    }

    private sealed class Lease(SessionReviewService owner) : IDisposable
    {
        private SessionReviewService? _owner = owner;
        public void Dispose()
        {
            if (_owner is null) return;
            _owner._leases--;
            if (_owner._leases == 0) { _owner._timer.Stop(); _owner._request?.Cancel(); }
            _owner = null;
        }
    }
}
