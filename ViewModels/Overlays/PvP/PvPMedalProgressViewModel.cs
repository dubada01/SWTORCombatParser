using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia.Media;
using ReactiveUI;
using ScottPlot;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.PvP;
using SWTORCombatParser.Model.LogParsing;
using Color = Avalonia.Media.Color;
using Colors = Avalonia.Media.Colors;

namespace SWTORCombatParser.ViewModels.Overlays.PvP;

public class PvPMedalProgressViewModel : ReactiveObject
{
    private IImmutableSolidColorBrush activeBrush = Brushes.RoyalBlue;
    private IImmutableSolidColorBrush completeBrush = Brushes.SeaGreen;
    public IImmutableSolidColorBrush BarColor => MedalAcquired ? completeBrush : activeBrush;
    public string Unit { get; set; }

    public double PercentComplete
    {
        get => _percentComplete;
        set
        {
            this.RaiseAndSetIfChanged(ref _percentComplete, value);
            if (_percentComplete >= 1)
            {
                this.RaisePropertyChanged(nameof(MedalAcquired));
                this.RaisePropertyChanged(nameof(BarColor));
            }
        }
    }

    public string Name { get; set; }

    public double CurrentValue
    {
        get => _currentValue;
        set
        {
            this.RaiseAndSetIfChanged(ref _currentValue, value);
            PercentComplete = CurrentValue / TargetValue;
        }
    }

    public double TargetValue { get; set; }
    public bool MedalAcquired => PercentComplete >= 1;

    public bool IsWarzoneKillTracker { get; set; }

    private double _currentValue;
    private double _percentComplete;
    private readonly MedalLogic _currentMedalType;

    private Dictionary<Entity, List<DateTime>> _countedPlayerKills = new Dictionary<Entity, List<DateTime>>();
    private Dictionary<Entity, List<DateTime>> _timeSawPlayerAlive = new Dictionary<Entity, List<DateTime>>();

    private double _max;
    private double _total;

    private double _current;

    private DateTime _currentCombatStartTime;

    public PvPMedalProgressViewModel(PvPMedal pvpMedal)
    {
        Name = pvpMedal.MedalName;
        Unit = pvpMedal.ThresholdUnit;
        TargetValue = pvpMedal.MedalThreshold;

        _currentMedalType = pvpMedal.MedalLogic;
    }

    public PvPMedalProgressViewModel()
    {
        Name = "Warzone Kill Tracker";
        IsWarzoneKillTracker = true;
        Unit = "Kills";
        TargetValue = 10;

        _currentMedalType = MedalLogic.KillCount;
    }

    public void UpdateMedalProgress(Combat parsedCombat)
    {
        if (parsedCombat == null || parsedCombat.LocalPlayer == null)
            return;
        if (_currentCombatStartTime != parsedCombat.StartTime)
        {
            ResetCombat(parsedCombat);
        }

        if (_currentMedalType == MedalLogic.MaxDamage)
        {
            var currentMax = parsedCombat.MaxEffectiveDamage[parsedCombat.LocalPlayer];
            if (currentMax > _max)
            {
                _max = currentMax;
                SetValue(_max);
            }
        }

        if (_currentMedalType == MedalLogic.MaxHeal)
        {
            var currentMax = parsedCombat.MaxEffectiveHeal[parsedCombat.LocalPlayer];
            if (currentMax > _max)
            {
                _max = currentMax;
                SetValue(_max);
            }
        }

        if (_currentMedalType == MedalLogic.TotalDamage)
        {
            _current = parsedCombat.TotalEffectiveDamage[parsedCombat.LocalPlayer];
            SetValue(_current + _total);
        }

        if (_currentMedalType == MedalLogic.TotalHealing)
        {
            _current = parsedCombat.TotalEffectiveHealing[parsedCombat.LocalPlayer];
            SetValue(_current + _total);
        }

        if (_currentMedalType == MedalLogic.KillCount)
        {
            AddKillsForPlayer(parsedCombat, parsedCombat.LocalPlayer);

            if (CombatLogStateBuilder.CurrentState
                    .GetCharacterClassAtTime(parsedCombat.LocalPlayer, parsedCombat.StartTime)
                    .Role == Role.Healer)
            {
                var opponents = parsedCombat.PvPOpponents.ToList();
                var allies = parsedCombat.CharacterParticipants.ToList().Where(c => !opponents.Contains(c.Value));
                foreach (var ally in allies)
                {
                    var healing = parsedCombat.OutgoingHealingLogs[parsedCombat.LocalPlayer].ToList();
                    if (healing.Any(l => l.Target == ally.Value))
                    {
                        AddKillsForPlayer(parsedCombat, ally.Value);
                    }
                }
            }

            SetValue(_current + _total);
        }
    }

    private void AddKillsForPlayer(Combat parsedCombat, Entity player)
    {
        // 1) GLOBAL: player combat segment starts (EnterCombatId logs live in AllLogs)
        // If you can filter EnterCombat to "local player only", do it here (recommended).
        var playerCombatStarts = parsedCombat.AllLogs.Values
            .Where(l => l.Effect.EffectId == _7_0LogParsing.EnterCombatId)
            .Select(l => l.TimeStamp)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        // 2) Precompute damage timestamps per opponent for fast window checks
        var outgoingDamageByTarget = parsedCombat.GetOutgoingDamageByTarget(player);
        var damageTimesByOpponent = BuildDamageTimesByOpponent(outgoingDamageByTarget);

        // 3) Iterate opponents and detect Alive -> Dead transitions (dedup death noise)
        foreach (var opponent in parsedCombat.PvPOpponents.ToList())
        {
            if (!damageTimesByOpponent.TryGetValue(opponent, out var playerDamageTimes))
                continue; // player never damaged them (effective > 0) in this overall combat parse

            var logsForEntity = parsedCombat.GetLogsInvolvingEntity(opponent)
                .OrderBy(l => l.TimeStamp)
                .ToList();

            ScanOpponentForDeathsAndCountKills(
                opponent,
                logsForEntity,
                playerDamageTimes,
                playerCombatStarts);
        }
    }

// --- Core scanning logic ---
    private void ScanOpponentForDeathsAndCountKills(
        Entity opponent,
        List<ParsedLogEntry> logsForOpponent,
        List<DateTime> sortedPlayerDamageTimes,
        List<DateTime> sortedPlayerCombatStarts)
    {
        DateTime? lifeStartTs = null;
        DateTime? lastSeenAlive = null;
        bool isDead = false;

        foreach (var log in logsForOpponent)
        {
            uint? hp = GetHpForEntityFromLog(log, opponent);
            if (hp is null) continue;

            // Alive evidence
            if (hp.Value > 0)
            {
                lastSeenAlive = log.TimeStamp;

                // If we were dead, this is a respawn/new life start
                if (isDead)
                    lifeStartTs = log.TimeStamp;

                // If we don't have a life start yet (first time we see them alive this segment), set it
                if (lifeStartTs is null)
                    lifeStartTs = log.TimeStamp;

                isDead = false;
                continue;
            }

            // Dead evidence (hp == 0)
            if (hp.Value == 0 && !isDead)
            {
                if (!IsValidDeathCandidate(log, opponent)) continue;
                if (lastSeenAlive is null) continue;

                // proof-of-life recency gate (prevents counting corpse spam / stale 0HP)
                const int aliveRecencySeconds = 30;
                if ((log.TimeStamp - lastSeenAlive.Value).TotalSeconds > aliveRecencySeconds)
                    continue;

                var segmentStart = GetLastCombatStartAtOrBefore(sortedPlayerCombatStarts, log.TimeStamp);

                // CREDIT WINDOW START: segmentStart + lifeStartTs (NOT lastSeenAlive)
                var windowStart = segmentStart ?? DateTime.MinValue;
                if (lifeStartTs is not null && lifeStartTs.Value > windowStart)
                    windowStart = lifeStartTs.Value;

                if (HasDamageInWindow(sortedPlayerDamageTimes, windowStart, log.TimeStamp))
                    CountKillOnce(opponent, log.TimeStamp);

                isDead = true;
            }
        }

    }

// --- Build a fast opponent->timestamps lookup ---
    private Dictionary<Entity, List<DateTime>> BuildDamageTimesByOpponent(
        Dictionary<Entity, ConcurrentQueue<ParsedLogEntry>> outgoingDamageByTarget)
    {
        var result = new Dictionary<Entity, List<DateTime>>();

        foreach (var kvp in outgoingDamageByTarget)
        {
            var opponent = kvp.Key;
            var times = kvp.Value
                .Where(l => l.Value.EffectiveDblValue > 0) // IMPORTANT: only real damage tags
                .Select(l => l.TimeStamp)
                .OrderBy(t => t)
                .ToList();

            if (times.Count > 0)
                result[opponent] = times;
        }

        return result;
    }

// --- Death candidate filtering (your existing exclusions) ---
    private bool IsValidDeathCandidate(ParsedLogEntry log, Entity opponent)
    {
        // True "death" log if it exists
        if (log.Target == opponent && log.Effect.EffectId == _7_0LogParsing.DeathCombatId)
            return true;

        // Otherwise we are using HP==0 as death signal; filter out noisy effects
        if (log.Effect.EffectId == _7_0LogParsing.TargetSetId) return false;
        if (log.Effect.EffectId == _7_0LogParsing.TargetClearedId) return false;
        if (log.Effect.EffectType == EffectType.Remove) return false;
        if (log.Target == log.Source) return false;

        // Also keep your "not (Target==Source)" style noise filters:
        // (You already had checks like l.Target != l.Source; this preserves that intent.)
        return true;
    }

// --- Pull HP for the opponent out of a log ---
    private uint? GetHpForEntityFromLog(ParsedLogEntry log, Entity entity)
    {
        if (log.Source == entity)
            return log.SourceInfo?.CurrentHP;

        if (log.Target == entity)
            return log.TargetInfo?.CurrentHP;

        return null;
    }

// --- Count kill with dedupe window ---
    private void CountKillOnce(Entity opponent, DateTime deathTs)
    {
        if (!_countedPlayerKills.TryGetValue(opponent, out var list))
        {
            list = new List<DateTime>();
            _countedPlayerKills[opponent] = list;
        }

        // Your existing dedupe window
        const int dedupeSeconds = 30;
        if (!list.Any(kts => Math.Abs((kts - deathTs).TotalSeconds) < dedupeSeconds))
        {
            list.Add(deathTs);
            _current += 1;
        }
    }

// --- Window check using binary search on sorted timestamps ---
    private bool HasDamageInWindow(List<DateTime> sortedDamageTimes, DateTime windowStartExclusive,
        DateTime deathInclusive)
    {
        // any t where windowStart < t <= death
        int idx = UpperBound(sortedDamageTimes, windowStartExclusive);
        return idx < sortedDamageTimes.Count && sortedDamageTimes[idx] <= deathInclusive;
    }

// First index where list[i] > value
    private int UpperBound(List<DateTime> list, DateTime value)
    {
        int lo = 0, hi = list.Count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            if (list[mid] <= value) lo = mid + 1;
            else hi = mid;
        }

        return lo;
    }

// Most recent combat start <= ts
    private DateTime? GetLastCombatStartAtOrBefore(List<DateTime> sortedStarts, DateTime ts)
    {
        if (sortedStarts.Count == 0) return null;

        int idx = UpperBound(sortedStarts, ts) - 1; // last <= ts
        return idx >= 0 ? sortedStarts[idx] : (DateTime?)null;
    }

    private void ResetCombat(Combat parsedCombat)
    {
        _total += _current;
        _current = 0;
        _currentCombatStartTime = parsedCombat.StartTime;
        _countedPlayerKills = new Dictionary<Entity, List<DateTime>>();
    }

    private void SetValue(double value)
    {
        CurrentValue = value;
        if (IsWarzoneKillTracker && (int)CurrentValue == (int)TargetValue)
        {
            TargetValue += 10;
        }
    }
}