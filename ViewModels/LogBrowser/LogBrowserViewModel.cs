using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Model.LogIndex;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.LogBrowser;

/// <summary>A metadata-only browser; opening a result is the only action that runs the combat parser.</summary>
public sealed class LogBrowserViewModel : ReactiveObject, IDisposable
{
    public const string All = "All";
    private readonly CombatLogIndexService _index;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _searchCancellation;
    private IReadOnlyList<IndexedCombatLog> _logs = Array.Empty<IndexedCombatLog>();
    private IReadOnlyList<LogFileCardViewModel> _results = Array.Empty<LogFileCardViewModel>();
    private LogFileCardViewModel? _selectedLog;
    private LogReplayChoice? _selectedReplay;
    public bool ReplayAvailable => DebugFeatures.LogReplayEnabled;
    public bool SimulatePartialWrite { get; set; }
    public IReadOnlyList<LogReplayChoice> ReplayChoices => SelectedLog?.ReplayChoices ?? Array.Empty<LogReplayChoice>();
    public LogReplayChoice? SelectedReplay
    {
        get => _selectedReplay;
        set => this.RaiseAndSetIfChanged(ref _selectedReplay, value);
    }
    public LogReplayRequest? CreateReplayRequest() => ReplayAvailable && SelectedLog != null && SelectedReplay != null
        ? new LogReplayRequest(SelectedLog.FilePath, SelectedReplay.From, SelectedReplay.Through, SimulatePartialWrite) : null;
    private string _activity = All, _encounter = All, _boss = All, _difficulty = All, _groupSize = All;
    private string _playerName = "", _status = "Reading saved log library…", _error = "";
    private DateTime? _fromDate, _toDate;
    private bool _isIndexing, _disposed, _changingOptions, _refreshingChoices;
    private int _searchGeneration;
    private IReadOnlyList<string> _encounters = new[] { All }, _bosses = new[] { All };
    private IReadOnlyList<string> _difficulties = new[] { All }, _groupSizes = new[] { All };

    public LogBrowserViewModel(string directory, CombatLogIndexService? index = null)
    {
        DirectoryPath = directory ?? "";
        _index = index ?? CombatLogIndexService.Shared;
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync,
            this.WhenAnyValue(vm => vm.IsIndexing, busy => !busy));
        ClearFiltersCommand = ReactiveCommand.Create(ClearFilters);
    }

    public string DirectoryPath { get; }
    public IReadOnlyList<string> ActivityTypes { get; } = new[] { All, "Operation", "Flashpoint", "PvP", "Other" };
    public IReadOnlyList<string> Encounters { get => _encounters; private set => this.RaiseAndSetIfChanged(ref _encounters, value); }
    public IReadOnlyList<string> Bosses { get => _bosses; private set => this.RaiseAndSetIfChanged(ref _bosses, value); }
    public IReadOnlyList<string> Difficulties { get => _difficulties; private set => this.RaiseAndSetIfChanged(ref _difficulties, value); }
    public IReadOnlyList<string> GroupSizes { get => _groupSizes; private set => this.RaiseAndSetIfChanged(ref _groupSizes, value); }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    public IReadOnlyList<LogFileCardViewModel> Results
    {
        get => _results;
        private set
        {
            this.RaiseAndSetIfChanged(ref _results, value);
            this.RaisePropertyChanged(nameof(ResultSummary));
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }

    public LogFileCardViewModel? SelectedLog
    {
        get => _selectedLog;
        set
        {
            if (ReferenceEquals(_selectedLog, value)) return;
            var previousChoice = _selectedReplay;
            var sameFile = _selectedLog?.FilePath == value?.FilePath;
            this.RaiseAndSetIfChanged(ref _selectedLog, value);
            this.RaisePropertyChanged(nameof(ReplayChoices));
            SelectedReplay = (sameFile ? ReplayChoices.FirstOrDefault(choice => choice == previousChoice) : null) ?? ReplayChoices.FirstOrDefault();
            this.RaisePropertyChanged(nameof(CanOpen));
            this.RaisePropertyChanged(nameof(HasSelection));
            this.RaisePropertyChanged(nameof(SelectedSequence));
            this.RaisePropertyChanged(nameof(SelectedPlayers));
            this.RaisePropertyChanged(nameof(SelectedSequenceTitle));
        }
    }

    public string ActivityType
    {
        get => _activity;
        set
        {
            if (_refreshingChoices || _activity == value || value == null) return;
            this.RaiseAndSetIfChanged(ref _activity, value);
            UpdateFilterOptions();
            QueueSearch();
        }
    }
    public string EncounterName
    {
        get => _encounter;
        set
        {
            if (_refreshingChoices || _encounter == value || value == null) return;
            this.RaiseAndSetIfChanged(ref _encounter, value);
            UpdateFilterOptions();
            QueueSearch();
        }
    }
    public string BossName { get => _boss; set { if (_refreshingChoices || value == null || value == _boss) return; this.RaiseAndSetIfChanged(ref _boss, value); QueueSearch(); } }
    public string Difficulty { get => _difficulty; set { if (_refreshingChoices || value == null || value == _difficulty) return; this.RaiseAndSetIfChanged(ref _difficulty, value); QueueSearch(); } }
    public string GroupSize { get => _groupSize; set { if (_refreshingChoices || value == null || value == _groupSize) return; this.RaiseAndSetIfChanged(ref _groupSize, value); QueueSearch(); } }
    public string PlayerName { get => _playerName; set { this.RaiseAndSetIfChanged(ref _playerName, value ?? ""); QueueSearch(); } }
    public DateTime? FromDate { get => _fromDate; set { this.RaiseAndSetIfChanged(ref _fromDate, value); QueueSearch(); } }
    public DateTime? ToDate { get => _toDate; set { this.RaiseAndSetIfChanged(ref _toDate, value); QueueSearch(); } }
    public bool IsIndexing { get => _isIndexing; private set { this.RaiseAndSetIfChanged(ref _isIndexing, value); this.RaisePropertyChanged(nameof(IsEmpty)); } }
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public string Error { get => _error; private set { this.RaiseAndSetIfChanged(ref _error, value); this.RaisePropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool IsEmpty => Results.Count == 0 && !IsIndexing;
    public bool CanOpen => SelectedLog != null;
    public bool HasSelection => SelectedLog != null;
    public string ResultSummary => $"{Results.Count:N0} matching {(Results.Count == 1 ? "log" : "logs")} · {_logs.Count:N0} in library";
    public IReadOnlyList<LogCombatChipViewModel> SelectedSequence => SelectedLog?.Sequence ?? Array.Empty<LogCombatChipViewModel>();
    public string SelectedPlayers => SelectedLog?.PlayerSummary ?? "";
    public string SelectedSequenceTitle => SelectedLog == null ? "Combat order" : $"Combat order · {SelectedLog.Sequence.Count:N0} entries";

    public async Task InitializeAsync()
    {
        try
        {
            var cached = await Task.Run(() => _index.GetCached(DirectoryPath), _lifetime.Token);
            if (_disposed) return;
            SetLogs(cached);
            await RefreshAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) { Error = "Could not read the log library: " + ex.Message; Status = "Use Browse file to open a log directly."; } }
    }

    public async Task RefreshAsync()
    {
        if (_disposed || IsIndexing) return;
        if (!Directory.Exists(DirectoryPath))
        {
            Error = "The combat log folder is unavailable. Check Preferences or browse to a file.";
            Status = "No combat log folder found.";
            return;
        }

        IsIndexing = true;
        Error = "";
        Status = "Checking for new and changed logs…";
        LogIndexProgress? latestProgress = null;
        var progress = new IndexProgress(update =>
        {
            // Capture completion synchronously so the final failure/save status cannot race UI dispatch.
            latestProgress = update;
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || !IsIndexing) return;
                if (update.AvailableLogs != null) SetLogs(update.AvailableLogs);
                Status = $"{update.ProcessedFiles:N0} / {update.TotalFiles:N0} files · {update.IndexedFiles:N0} updated · {update.ReusedFiles:N0} unchanged · {update.Message}";
            });
        });
        try
        {
            var logs = await _index.RefreshAsync(DirectoryPath, progress, _lifetime.Token);
            if (_disposed) return;
            SetLogs(logs);
            Status = $"{logs.Count:N0} logs · {latestProgress?.Message ?? "Library up to date."}";
            if (latestProgress?.FailedFiles > 0) Error = $"{latestProgress.FailedFiles:N0} files could not be checked. Saved metadata remains available where possible; refresh to retry.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed) { Error = "Could not update the log library: " + ex.Message; Status = "Saved results are still available."; } }
        finally { if (!_disposed) IsIndexing = false; }
    }

    public void ReportOpenError(string message) => Error = message;

    private void SetLogs(IReadOnlyList<IndexedCombatLog> logs)
    {
        if (ReferenceEquals(_logs, logs)) return;
        _logs = logs;
        UpdateFilterOptions();
        // Index results are already throttled; don't debounce them behind the next incoming batch.
        QueueSearch(debounce: false);
    }

    private void UpdateFilterOptions()
    {
        if (_changingOptions) return;
        _changingOptions = true;
        _refreshingChoices = true;
        try
        {
            var encounters = _logs.SelectMany(log => log.Encounters)
                .Where(item => ActivityType == All || item.ActivityType == ActivityType).ToList();
            var encounterChoices = Choices(encounters.Select(item => item.EncounterName));
            if (!Encounters.SequenceEqual(encounterChoices)) Encounters = encounterChoices;
            if (!Encounters.Contains(_encounter)) { _encounter = All; this.RaisePropertyChanged(nameof(EncounterName)); }
            var selectedEncounters = encounters.Where(item => EncounterName == All || item.EncounterName == EncounterName).ToList();
            var bossChoices = Choices(selectedEncounters.Select(item => item.BossName));
            var difficultyChoices = Choices(selectedEncounters.Select(item => item.Difficulty));
            var groupChoices = new[] { All }.Concat(selectedEncounters.Select(item => item.PlayerCount).Where(size => size > 0)
                .Distinct().OrderBy(size => size).Select(size => size.ToString())).ToArray();
            if (!Bosses.SequenceEqual(bossChoices)) Bosses = bossChoices;
            if (!Difficulties.SequenceEqual(difficultyChoices)) Difficulties = difficultyChoices;
            if (!GroupSizes.SequenceEqual(groupChoices)) GroupSizes = groupChoices;
            if (!Bosses.Contains(_boss)) { _boss = All; this.RaisePropertyChanged(nameof(BossName)); }
            if (!Difficulties.Contains(_difficulty)) { _difficulty = All; this.RaisePropertyChanged(nameof(Difficulty)); }
            if (!GroupSizes.Contains(_groupSize)) { _groupSize = All; this.RaisePropertyChanged(nameof(GroupSize)); }
        }
        finally { _refreshingChoices = false; _changingOptions = false; }
    }

    private static IReadOnlyList<string> Choices(IEnumerable<string> source) => new[] { All }
        .Concat(source.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase)).ToArray();

    private void ClearFilters()
    {
        _changingOptions = true;
        ActivityType = All;
        EncounterName = All;
        BossName = All;
        Difficulty = All;
        GroupSize = All;
        PlayerName = "";
        FromDate = null;
        ToDate = null;
        _changingOptions = false;
        UpdateFilterOptions();
        QueueSearch();
    }

    private void QueueSearch(bool debounce = true)
    {
        if (_disposed || _changingOptions) return;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var generation = ++_searchGeneration;
        var filter = new CombatLogSearchFilter
        {
            ActivityType = ActivityType == All ? null : ActivityType,
            EncounterName = EncounterName == All ? null : EncounterName,
            BossName = BossName == All ? null : BossName,
            Difficulty = Difficulty == All ? null : Difficulty,
            PlayerCount = int.TryParse(GroupSize, out var size) ? size : null,
            PlayerName = string.IsNullOrWhiteSpace(PlayerName) ? null : PlayerName.Trim(),
            FromDate = FromDate,
            ToDate = ToDate
        };
        _ = SearchAsync(_logs, filter, generation, _searchCancellation.Token, debounce);
    }

    private async Task SearchAsync(IReadOnlyList<IndexedCombatLog> logs, CombatLogSearchFilter filter, int generation, CancellationToken token, bool debounce)
    {
        try
        {
            if (debounce) await Task.Delay(180, token);
            var existingCards = Results.ToDictionary(card => card.FilePath, StringComparer.Ordinal);
            var invalidDates = filter.FromDate?.Date > filter.ToDate?.Date;
            var results = await Task.Run(() => invalidDates
                ? Array.Empty<LogFileCardViewModel>()
                : CombatLogSearch.Search(logs, filter).Select(log =>
                {
                    token.ThrowIfCancellationRequested();
                    return existingCards.TryGetValue(log.FilePath, out var card) && ReferenceEquals(card.Source, log)
                        ? card : new LogFileCardViewModel(log);
                }).ToArray(), token);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_disposed || token.IsCancellationRequested || generation != _searchGeneration) return;
                var selectedPath = SelectedLog?.FilePath;
                var selectedReplay = SelectedReplay;
                Results = results;
                SelectedLog = results.FirstOrDefault(result => result.FilePath == selectedPath);
                if (SelectedLog != null && selectedReplay != null)
                    SelectedReplay = ReplayChoices.FirstOrDefault(choice => choice == selectedReplay) ?? ReplayChoices.FirstOrDefault();
                if (invalidDates) Error = "Choose an end date on or after the start date.";
                else if (Error == "Choose an end date on or after the start date.") Error = "";
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { if (!_disposed && generation == _searchGeneration) Error = "Could not filter logs: " + ex.Message; });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        RefreshCommand.Dispose();
        ClearFiltersCommand.Dispose();
        _lifetime.Dispose();
    }

    private sealed class IndexProgress(Action<LogIndexProgress> report) : IProgress<LogIndexProgress>
    {
        public void Report(LogIndexProgress value) => report(value);
    }
}

public sealed class LogFileCardViewModel
{
    internal IndexedCombatLog Source { get; }
    private readonly IndexedEncounter[] _encounters;
    private IReadOnlyList<LogCombatChipViewModel>? _sequence;
    private IReadOnlyList<LogReplayChoice>? _replayChoices;

    public LogFileCardViewModel(IndexedCombatLog log)
    {
        Source = log;
        FilePath = log.FilePath;
        FileName = log.FileName;
        DateLabel = log.StartTime.ToString("ddd, MMM d, yyyy · h:mm tt");
        var names = log.Encounters.Select(item => item.EncounterName).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToList();
        Title = names.Count == 0 ? "Unidentified session" : string.Join(" · ", names);
        var activities = log.Encounters.Select(item => item.ActivityType).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct();
        Metadata = string.Join(" · ", activities.Append($"{log.Encounters.Count:N0} combats").Append(FormatSize(log.FileSize)));
        PlayerSummary = log.Players.Count == 0 ? "No player names found" : "Players: " + string.Join(", ", log.Players);
        _encounters = log.Encounters.OrderBy(item => item.StartTime).ToArray();
        // Keep cards compact; the selected file's complete chronological sequence remains available below.
        var important = _encounters.Select((item, index) => (item, number: index + 1))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.item.BossName) || pair.item.ActivityType == "PvP").ToArray();
        if (important.Length == 0) important = _encounters.Select((item, index) => (item, number: index + 1)).ToArray();
        Preview = important.Take(8).Select(pair => new LogCombatChipViewModel(pair.item, pair.number)).ToArray();
        MoreLabel = important.Length > Preview.Count ? $"+{important.Length - Preview.Count} more · select to view full combat order" : "";
        HasMore = MoreLabel.Length > 0;
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string DateLabel { get; }
    public string Title { get; }
    public string Metadata { get; }
    public string PlayerSummary { get; }
    public string MoreLabel { get; }
    public bool HasMore { get; }
    public IReadOnlyList<LogCombatChipViewModel> Preview { get; }
    public IReadOnlyList<LogCombatChipViewModel> Sequence => _sequence ??=
        _encounters.Select((item, index) => new LogCombatChipViewModel(item, index + 1)).ToArray();
    public IReadOnlyList<LogReplayChoice> ReplayChoices => _replayChoices ??= new[] { new LogReplayChoice("Entire log · original timing") }
        .Concat(_encounters.Select((item, index) => new LogReplayChoice(
            $"{index + 1:00} · {item.StartTime:HH:mm:ss} · {(string.IsNullOrEmpty(item.BossName) ? item.EncounterName : item.BossName)} · {item.Outcome}",
            item.StartTime, item.EndTime))).ToArray();
    private static string FormatSize(long size) => size >= 1024 * 1024 ? $"{size / 1024d / 1024:N1} MB" : $"{size / 1024d:N0} KB";
}

public sealed class LogCombatChipViewModel
{
    public LogCombatChipViewModel(IndexedEncounter encounter, int number)
    {
        Number = number.ToString("00");
        Name = !string.IsNullOrWhiteSpace(encounter.BossName) ? encounter.BossName
            : encounter.ActivityType == "PvP" ? encounter.EncounterName : "Combat";
        IsImportant = !string.IsNullOrWhiteSpace(encounter.BossName) || encounter.ActivityType == "PvP";
        Outcome = encounter.Outcome;
        OutcomeBrush = encounter.Outcome == "Kill" ? Brushes.MediumSeaGreen
            : encounter.Outcome == "Attempt" ? Brushes.SandyBrown : Brushes.LightSlateGray;
        var duration = encounter.EndTime - encounter.StartTime;
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        Detail = $"{encounter.StartTime:HH:mm:ss} · {(int)duration.TotalMinutes}:{duration.Seconds:00} · {encounter.Outcome}";
        Tooltip = string.Join(" · ", new[] { encounter.EncounterName, encounter.Difficulty,
            encounter.PlayerCount > 0 ? $"{encounter.PlayerCount} players" : "Group size unknown", Detail }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    public string Number { get; }
    public string Name { get; }
    public string Outcome { get; }
    public string Detail { get; }
    public string Tooltip { get; }
    public bool IsImportant { get; }
    public IBrush OutcomeBrush { get; }
}
