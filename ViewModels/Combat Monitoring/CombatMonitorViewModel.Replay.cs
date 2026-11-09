using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using ReactiveUI;
using Avalonia.Threading;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Combat_Monitoring;

public partial class CombatMonitorViewModel
{
    private CancellationTokenSource? _replayCancellation;
    private Task _replayTask = Task.CompletedTask;
    private bool _isReplaying;
    private string _replayStatus = "";
    private ReactiveCommand<Unit, Unit>? _stopReplayCommand;
    public bool IsReplaying
    {
        get => _isReplaying;
        private set { _isReplaying = value; OnPropertyChanged(); }
    }
    public string ReplayStatus
    {
        get => _replayStatus;
        private set { _replayStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasReplayStatus)); }
    }
    public bool HasReplayStatus => DebugFeatures.LogReplayEnabled && ReplayStatus.Length > 0;
    public ReactiveCommand<Unit, Unit> StopReplayCommand => _stopReplayCommand ??= ReactiveCommand.CreateFromTask(StopReplayAsync);

    private async Task StartReplayAsync(LogReplayRequest request)
    {
        if (!DebugFeatures.LogReplayEnabled) throw new InvalidOperationException("Log replay is available only in Debug builds.");
        await StopReplayAsync();
        Interlocked.Increment(ref _monitorRequestGeneration);
        IsReplaying = true;
        ReplayStatus = "Preparing replay…";
        _replayCancellation = new CancellationTokenSource();
        _replayTask = RunReplayAsync(request, _replayCancellation.Token);
    }

    private async Task RunReplayAsync(LogReplayRequest request, CancellationToken token)
    {
        CombatLogReplay? replay = null;
        LogReplayContext? context = null;
        bool monitoring = false;
        var result = $"Replay complete · {request.SpeedMultiplier:0.##}×";
        try
        {
            await _combatLogStreamer.StopMonitoring();
            LiveParseActive = false;
            OnMonitoringStateChanged(false);
            token.ThrowIfCancellationRequested();
            replay = await CombatLogReplay.PrepareAsync(request, token);
            token.ThrowIfCancellationRequested();
            var anchor = TimeUtility.CorrectedTime;
            context = LogReplayContext.Begin(anchor, request.SpeedMultiplier);
            await replay.WritePreludeAsync(anchor, token);
            token.ThrowIfCancellationRequested();
            Reset();
            CurrentlySelectedLogName = request.FilePath;
            OnPropertyChanged(nameof(CurrentlySelectedLogName));
            ReplayStatus = "Restoring encounter context…";
            LiveParseActive = true;
            OnMonitoringStateChanged(true);
            monitoring = true;
            await _combatLogStreamer.MonitorReplayAsync(replay.OutputPath, replay.ParseDate(anchor), token);
            token.ThrowIfCancellationRequested();
            context.Start();
            var progress = new Progress<LogReplayProgress>(update =>
            {
                if (token.IsCancellationRequested || !IsReplaying) return;
                ReplayStatus = $"DEBUG · {request.SpeedMultiplier:0.##}× · {FormatReplayTime(update.Elapsed)} / {FormatReplayTime(update.Duration)} · {Path.GetFileName(request.FilePath)}";
            });
            await Task.Run(() => replay.PlayAsync(anchor, progress, token), token);
        }
        catch (OperationCanceledException) { result = "Replay stopped"; }
        catch (Exception error)
        {
            result = "Replay failed: " + error.Message;
            Logging.LogError(result);
        }
        finally
        {
            // Keep replay identity and the simulation clock active until final events have been consumed.
            try { if (monitoring) await _combatLogStreamer.StopMonitoring(readFinalFrame: true); }
            catch (Exception error) { result = "Replay stopped: " + error.Message; }
            if (context != null)
            {
                // Cooldowns that survive combat belong to the simulated clock.
                // Finish them before restoring real time, which can be hours
                // behind the end of an accelerated replay.
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(TimerController.CompleteActiveTimersAsync);
                }
                catch (Exception error) { result = "Replay stopped: " + error.Message; }
            }
            if (monitoring)
            {
                LiveParseActive = false;
                OnMonitoringStateChanged(false);
            }
            context?.Dispose();
            replay?.Dispose();
            IsReplaying = false;
            ReplayStatus = result;
        }
    }

    public async Task StopReplayAsync()
    {
        var cancellation = _replayCancellation;
        var task = _replayTask;
        cancellation?.Cancel();
        await task;
        if (ReferenceEquals(_replayCancellation, cancellation))
        {
            cancellation?.Dispose();
            _replayCancellation = null;
        }
    }

    public void CancelReplay() => _replayCancellation?.Cancel();
    private static string FormatReplayTime(TimeSpan time) => time.TotalHours >= 1
        ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}" : $"{(int)time.TotalMinutes}:{time.Seconds:00}";
}
