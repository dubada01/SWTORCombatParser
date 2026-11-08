using System;
using ReactiveUI;
using SWTORCombatParser.DataStructures.SessionReview;

namespace SWTORCombatParser.ViewModels.SessionReview;

public sealed class SessionPerformanceViewModel : ReactiveObject
{
    public SessionReviewSnapshot? Snapshot { get; private set; }
    private SessionPlayerMetrics _metrics = new();
    private double _playerSeconds;
    public bool HasData => Snapshot?.Combats.Count > 0;
    public string Title => Snapshot?.Name ?? "Session Summary";
    public string Details => Snapshot?.Details ?? "Load a log or start parsing";
    public string Elapsed => Duration(Snapshot?.ElapsedSeconds ?? 0);
    public string CombatTime => Duration(Snapshot?.CombatSeconds ?? 0);
    public string Downtime => Duration(Snapshot?.DowntimeSeconds ?? 0);
    public string Damage => _metrics.Damage.ToString("N0");
    public string Healing => _metrics.EffectiveHealing.ToString("N0");
    public string DamageTaken => _metrics.DamageTaken.ToString("N0");
    public string Deaths => _metrics.Deaths.ToString("N0");
    public string PlayerCombatTime => Duration(_playerSeconds);
    public string CombatDps => Rate(_metrics.Damage, _playerSeconds).ToString("N0");
    public string CombatEhps => Rate(_metrics.EffectiveHealing, _playerSeconds).ToString("N0");
    public string CombatDtps => Rate(_metrics.DamageTaken, _playerSeconds).ToString("N0");
    public string SessionDps => Rate(_metrics.Damage, Snapshot?.ElapsedSeconds ?? 0).ToString("N0");
    public string Progress => Snapshot is null ? "No session selected" : $"{Snapshot.Kills} kills · {Snapshot.Wipes} wipes · {Snapshot.Combats.Count} combats";
    public string Range => !HasData ? "" : $"{Snapshot!.Start:g} — {Snapshot.End:T}";
    public string Activity => Snapshot?.ElapsedSeconds > 0 ? $"{Snapshot.CombatSeconds / Snapshot.ElapsedSeconds:P0} in combat" : "No combat time";

    public void Update(SessionReviewSnapshot? snapshot, string? player = null)
    {
        Snapshot = snapshot;
        _metrics = snapshot?.ForPlayer(player) ?? new SessionPlayerMetrics();
        _playerSeconds = snapshot?.PlayerCombatSeconds(player) ?? 0;
        foreach (var property in new[] { nameof(Snapshot), nameof(HasData), nameof(Title), nameof(Details), nameof(Elapsed),
                     nameof(CombatTime), nameof(Downtime), nameof(Damage), nameof(Healing), nameof(DamageTaken), nameof(Deaths),
                     nameof(CombatDps), nameof(CombatEhps), nameof(CombatDtps), nameof(PlayerCombatTime), nameof(SessionDps), nameof(Progress), nameof(Range), nameof(Activity) })
            this.RaisePropertyChanged(property);
    }

    public static double Rate(double value, double seconds) => seconds > 0 ? value / seconds : 0;
    public static string Duration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}:{duration:mm\\:ss}" : duration.ToString(@"mm\:ss");
    }
}
