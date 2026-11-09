using System;
using System.Collections.Generic;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.Model.CombatCaching;

// Keep a small working set for A/B comparisons, not the entire raid night.
// The event budget includes all retained fights, including the displayed one.
internal sealed class RecentCombatCache(int maxCombats = 3, int maxEvents = 200_000)
{
    private readonly object _gate = new();
    private readonly LinkedList<(string Path, Combat Combat, int Events)> _recent = new();
    private Guid _owner;
    private int _events;

    internal Combat? Get(string path, Guid owner)
    {
        lock (_gate)
        {
            UseOwner(owner);
            for (var node = _recent.First; node != null; node = node.Next)
            {
                if (node.Value.Path != path) continue;
                _recent.Remove(node); _recent.AddFirst(node);
                return node.Value.Combat;
            }
            return null;
        }
    }

    internal void Remember(string path, Combat combat)
    {
        lock (_gate)
        {
            UseOwner(combat.CacheLogStateId);
            for (var node = _recent.First; node != null; node = node.Next)
                if (node.Value.Path == path) { _events -= node.Value.Events; _recent.Remove(node); break; }
            var count = combat.AllLogs.Count;
            if (count <= 0 || count > maxEvents) return;
            _recent.AddFirst((path, combat, count)); _events += count;
            while (_recent.Count > maxCombats || _events > maxEvents)
            {
                _events -= _recent.Last!.Value.Events;
                _recent.RemoveLast();
            }
        }
    }

    internal void Clear()
    {
        lock (_gate) { _recent.Clear(); _events = 0; _owner = Guid.Empty; }
    }

    private void UseOwner(Guid owner)
    {
        if (_owner == owner) return;
        _recent.Clear(); _events = 0; _owner = owner;
    }
}
