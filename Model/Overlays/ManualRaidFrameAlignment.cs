using System;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.Model.Overlays;

public readonly record struct RaidFrameCell(int Row, int Column);

/// <summary>
/// Pairs a frame click with the subsequent local-player target selection.
/// A newer click replaces an unanswered click, since selecting the current
/// target does not produce another selection entry in the combat log.
/// </summary>
public sealed class ManualRaidFrameAlignment
{
    public static readonly TimeSpan SelectionTimeout = TimeSpan.FromSeconds(4);

    private readonly object _sync = new();
    private RaidFrameCell? _pendingCell;
    private DateTime _clickedAt;

    public RaidFrameCell? PendingCell
    {
        get
        {
            lock (_sync)
                return _pendingCell;
        }
    }

    public bool HasPendingClick => PendingCell.HasValue;

    public static bool IsPlayerSelection(ParsedLogEntry? entry) =>
        entry?.SourceInfo?.Entity?.IsLocalPlayer == true &&
        entry.TargetInfo?.Entity?.IsCharacter == true &&
        !string.IsNullOrWhiteSpace(entry.Target.Name) &&
        entry.Effect?.EffectType == EffectType.TargetChanged &&
        entry.Effect.EffectId == _7_0LogParsing.TargetSetId;

    public bool TryMatch(ParsedLogEntry? entry, out RaidFrameCell cell)
    {
        cell = default;
        return IsPlayerSelection(entry) && TryMatch(entry!.TimeStamp, out cell);
    }

    public bool RecordClick(int row, int column, DateTime timestamp)
    {
        if (row < 0 || column < 0)
            return false;

        lock (_sync)
        {
            _pendingCell = new RaidFrameCell(row, column);
            _clickedAt = timestamp;
            return true;
        }
    }

    public bool TryMatch(DateTime selectionTimestamp, out RaidFrameCell cell)
    {
        lock (_sync)
        {
            cell = default;
            if (!_pendingCell.HasValue)
                return false;

            var elapsed = selectionTimestamp - _clickedAt;
            // A delayed log entry from an earlier click must not claim the newer cell.
            if (elapsed < TimeSpan.Zero)
                return false;

            if (elapsed >= SelectionTimeout)
            {
                _pendingCell = null;
                return false;
            }

            cell = _pendingCell.Value;
            _pendingCell = null;
            return true;
        }
    }

    public bool Expire(DateTime now)
    {
        lock (_sync)
        {
            if (!_pendingCell.HasValue || now - _clickedAt < SelectionTimeout)
                return false;

            _pendingCell = null;
            return true;
        }
    }

    public void Clear()
    {
        lock (_sync)
            _pendingCell = null;
    }
}
