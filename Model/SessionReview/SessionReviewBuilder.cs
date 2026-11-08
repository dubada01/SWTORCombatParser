using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.SessionReview;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.Model.SessionReview;

/// <summary>Pure aggregation independent of live parser state, selected phases and leaderboards.</summary>
public static class SessionReviewBuilder
{
    public static SessionCombatSummary Summarize(string name, DateTime start, DateTime end,
        SessionCombatOutcome outcome, IEnumerable<ParsedLogEntry> logs, CancellationToken cancellationToken = default,
        bool? isBoss = null, IReadOnlyDictionary<string, SessionPlayerInfo>? playerInfo = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = logs as IReadOnlyCollection<ParsedLogEntry> ?? logs.ToArray();
        if (start == DateTime.MinValue)
            start = entries.Where(l => l.TimeStamp > DateTime.MinValue).Select(l => l.TimeStamp).DefaultIfEmpty(DateTime.MinValue).Min();
        var players = new Dictionary<string, SessionPlayerMetrics>(StringComparer.OrdinalIgnoreCase);
        var info = new Dictionary<string, SessionPlayerInfo>(StringComparer.OrdinalIgnoreCase);
        var samples = new Dictionary<string, Dictionary<DateTime, Sample>>(StringComparer.OrdinalIgnoreCase);
        var localPlayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasData = false;
        var first = DateTime.MaxValue;
        var last = DateTime.MinValue;
        // Some synthetic/merged logs repeat a death notification. Match target and timestamp.
        var deaths = new HashSet<(string Player, DateTime Time)>();
        foreach (var log in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Persisted raw caches omit synthetic absorbs. Treat loaded and unloaded combats identically.
            if (log.Effect?.EffectType == EffectType.AbsorbShield) continue;
            hasData = true;
            if (log.TimeStamp > DateTime.MinValue)
            {
                if (log.TimeStamp < first) first = log.TimeStamp;
                if (log.TimeStamp > last) last = log.TimeStamp;
            }
            var source = PlayerName(log.SourceInfo?.Entity);
            var target = PlayerName(log.TargetInfo?.Entity);
            Register(source, log.SourceInfo, log.TimeStamp);
            Register(target, log.TargetInfo, log.TimeStamp);
            var effect = log.Effect?.EffectId ?? 0;
            var value = log.Value?.EffectiveDblValue ?? 0;
            if (!double.IsFinite(value) || value < 0) value = 0;
            if (log.Effect?.EffectType == EffectType.Apply && effect == _7_0LogParsing._damageEffectId)
            {
                if (source is not null && log.SourceInfo?.Entity?.Name != log.TargetInfo?.Entity?.Name)
                    Add(source, log.TimeStamp, new SessionPlayerMetrics(Damage: value));
                var taken = log.Value?.MitigatedDblValue ?? 0;
                if (!double.IsFinite(taken) || taken < 0) taken = 0;
                if (target is not null) Add(target, log.TimeStamp, new SessionPlayerMetrics(DamageTaken: taken));
            }
            else if (log.Effect?.EffectType == EffectType.Apply && effect == _7_0LogParsing._healEffectId && source is not null
                     && log.AbilityId != 813707324030976 && log.AbilityId != 3289210509328384)
                Add(source, log.TimeStamp, new SessionPlayerMetrics(EffectiveHealing: value));
            else if (effect == _7_0LogParsing.DeathCombatId && target is not null && deaths.Add((target.ToUpperInvariant(), log.TimeStamp)))
                Add(target, log.TimeStamp, new SessionPlayerMetrics(Deaths: 1));
        }
        if (start == DateTime.MinValue && first != DateTime.MaxValue) start = first;
        if (end < start || end == DateTime.MinValue) end = last > start ? last : start;
        return new SessionCombatSummary(name, start, end, outcome, players, localPlayers.ToArray(), hasData)
        {
            IsBoss = isBoss ?? outcome is SessionCombatOutcome.Kill or SessionCombatOutcome.Wipe,
            PlayerInfo = info,
            Samples = samples.ToDictionary(k => k.Key, k => (IReadOnlyList<SessionMetricSample>)k.Value.OrderBy(s => s.Key)
                .Select(s => new SessionMetricSample(s.Key, s.Value.Metrics, s.Value.Health, s.Value.HealthPercent)).ToArray(), StringComparer.OrdinalIgnoreCase)
        };

        void Register(string? player, EntityInfo? entityInfo, DateTime time)
        {
            if (player is null) return;
            players.TryAdd(player, new SessionPlayerMetrics());
            if (entityInfo?.Entity?.IsLocalPlayer == true) localPlayers.Add(player);
            if (!info.ContainsKey(player)) info[player] = playerInfo?.GetValueOrDefault(player) ?? new SessionPlayerInfo();
            if (entityInfo?.Class?.Role is { } role && role != Role.Unknown)
                info[player] = new SessionPlayerInfo(role, entityInfo.Class.Discipline ?? "");
            if (entityInfo is { MaxHP: > 0 } && time >= start)
            {
                var sample = GetSample(player, time);
                // Logs can arrive out of order. Keep the last actual HP observation, not iteration order.
                if (time >= sample.HealthTime)
                {
                    sample.HealthTime = time;
                    sample.Health = Math.Min(entityInfo.CurrentHP, entityInfo.MaxHP);
                    sample.HealthPercent = 100d * sample.Health / entityInfo.MaxHP;
                }
            }
        }
        Sample GetSample(string player, DateTime time)
        {
            if (!samples.TryGetValue(player, out var buckets)) samples[player] = buckets = new Dictionary<DateTime, Sample>();
            var key = start.AddSeconds(Math.Max(0, Math.Floor((time - start).TotalSeconds)));
            if (!buckets.TryGetValue(key, out var sample)) buckets[key] = sample = new Sample();
            return sample;
        }
        void Add(string player, DateTime time, SessionPlayerMetrics metrics)
        {
            players[player] += metrics;
            var sample = GetSample(player, time);
            sample.Metrics += metrics;
        }
    }

    private sealed class Sample
    {
        public SessionPlayerMetrics Metrics = new();
        public DateTime HealthTime;
        public double? Health;
        public double? HealthPercent;
    }

    private static string? PlayerName(Entity? entity) => entity is { IsCharacter: true } &&
        !string.IsNullOrWhiteSpace(entity.Name) ? entity.Name : null;
}
