using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Phases;

namespace SWTORCombatParser.Model.Progression;

internal static class ProgressionSummaryBuilder
{
    internal const int HitWindowSeconds = 5;
    internal const int LeadupSeconds = 10;
    internal const int LeadupLimit = 12;

    internal static ProgressionPullSummary Build(ProgressionPullMetadata metadata, IReadOnlyList<ParsedLogEntry> orderedLogs,
        IEnumerable<Phase> definitions, string phaseRevision, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (orderedLogs.Count == 0)
            return ProgressionPullSummary.Placeholder(metadata, ProgressionDataState.Unavailable);
        var prepared = PhaseManager.Reconstruct(metadata.Boss, metadata.Start, metadata.End,
            orderedLogs.Where(log => log.TimeStamp >= metadata.Start && log.TimeStamp <= metadata.End),
            definitions, () => !token.IsCancellationRequested);
        token.ThrowIfCancellationRequested();
        // Raw intervals preserve gaps between repetitions of the same named phase.
        var phases = prepared!.Raw;
        var deaths = new List<(long Line, ProgressionDeath Death)>();
        var players = new Dictionary<(long Id, string Name), PlayerWindow>();
        foreach (var entry in orderedLogs)
        {
            token.ThrowIfCancellationRequested();
            if (entry.TimeStamp < metadata.Start || entry.TimeStamp > metadata.End ||
                entry.TargetInfo?.Entity is not { IsCharacter: true, IsCompanion: false } player ||
                string.IsNullOrWhiteSpace(player.Name) || entry.Effect is not { } effect)
                continue;

            var isDeath = effect.EffectId == _7_0LogParsing.DeathCombatId;
            var isRevive = effect.EffectId == _7_0LogParsing.RevivedCombatId;
            var isDamage = effect.EffectType == EffectType.Apply && effect.EffectId == _7_0LogParsing._damageEffectId;
            var isHeal = effect.EffectType == EffectType.Apply && effect.EffectId == _7_0LogParsing._healEffectId;
            if (!isDeath && !isRevive && !isDamage && !isHeal) continue;

            var key = (player.Id, player.Name);
            if (!players.TryGetValue(key, out var window)) players[key] = window = new PlayerWindow();
            if (isDeath)
            {
                if (!window.DeathTimes.Add(entry.TimeStamp)) continue;
                var reset = IsReset(entry.Ability);
                var hit = reset || string.IsNullOrWhiteSpace(entry.SourceInfo?.Entity?.Name) ? null : window.GetLastHit(entry);
                var leadup = window.Events.Where(item => item.Event.Time <= entry.TimeStamp &&
                    (entry.TimeStamp - item.Event.Time).TotalSeconds <= LeadupSeconds)
                    .Select(item => item.Event).OrderBy(item => item.Time).ToArray();
                deaths.Add((entry.LogLineNumber, new ProgressionDeath(player.Name, entry.TimeStamp,
                    PhaseAt(phases, entry.TimeStamp), hit, reset, leadup)));
                window.Clear();
                continue;
            }
            if (isRevive)
            {
                window.Clear();
                continue;
            }

            var amount = entry.Value?.EffectiveDblValue ?? 0;
            if (amount <= 0 || !double.IsFinite(amount)) continue;
            var sample = new ProgressionRecapEvent(entry.TimeStamp, isDamage ? ProgressionEventKind.Damage : ProgressionEventKind.Heal,
                entry.AbilityId, Known(entry.Ability), Known(entry.SourceInfo?.Entity?.Name), amount);
            window.Add(sample, entry.LogLineNumber);
        }

        return new ProgressionPullSummary(metadata, ProgressionDataState.Available,
            Known(phases.LastOrDefault()?.SourcePhase.Name), PhaseAt(phases, metadata.End), phaseRevision,
            deaths.OrderBy(item => item.Death.Time).ThenBy(item => item.Line).Select(item => item.Death).ToArray());
    }

    private static string? PhaseAt(List<PhaseInstance> phases, DateTime time) =>
        Known(phases.LastOrDefault(phase => phase.PhaseStart <= time &&
            (phase.PhaseEnd == default || time <= phase.PhaseEnd))?.SourcePhase.Name);

    private static bool IsReset(string? ability) => ability?.Trim().TrimStart('/').Equals("stuck", StringComparison.OrdinalIgnoreCase) == true
        || string.Equals(ability, "Reset", StringComparison.OrdinalIgnoreCase);

    private static string? Known(string? name) => string.IsNullOrWhiteSpace(name) ? null : name;

    private sealed class PlayerWindow
    {
        private (ProgressionRecapEvent Event, long Line)? _lastDamage;
        private bool _ambiguous;
        public readonly Queue<(ProgressionRecapEvent Event, long Line)> Events = new();
        public readonly HashSet<DateTime> DeathTimes = new();

        public void Add(ProgressionRecapEvent sample, long line)
        {
            while (Events.TryPeek(out var first) && (Events.Count >= LeadupLimit ||
                (sample.Time - first.Event.Time).TotalSeconds > LeadupSeconds)) Events.Dequeue();
            Events.Enqueue((sample, line));
            if (sample.Kind != ProgressionEventKind.Damage) return;
            if (_lastDamage is { } last)
            {
                if (last.Event.Time > sample.Time) return;
                if (last.Event.Time == sample.Time && (last.Line == line || last.Line <= 0 || line <= 0))
                {
                    _ambiguous |= last.Event != sample;
                    return;
                }
            }
            _lastDamage = (sample, line);
            _ambiguous = false;
        }

        public ProgressionRecapEvent? GetLastHit(ParsedLogEntry death)
        {
            if (_ambiguous || _lastDamage is not { } last || last.Event.Ability == null || last.Event.Source == null)
                return null;
            var gap = (death.TimeStamp - last.Event.Time).TotalSeconds;
            return gap >= 0 && gap <= HitWindowSeconds &&
                (gap != 0 || last.Line > 0 && last.Line < death.LogLineNumber) ? last.Event : null;
        }

        public void Clear()
        {
            Events.Clear();
            _lastDamage = null;
            _ambiguous = false;
        }
    }
}
