using System;
using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.Model.CombatParsing;

public static class EncounterSessionGrouping
{
    public static bool StartsNewSession(EncounterInfo? previous, EncounterInfo next,
        DateTime previousVisit, DateTime nextVisit)
    {
        if (previous == null || previous.Name != next.Name || previous.Difficutly != next.Difficutly ||
            previous.NumberOfPlayer != next.NumberOfPlayer)
            return true;

        return (next.IsBossEncounter || next.IsPvpEncounter) && previousVisit != nextVisit;
    }

    public static DateTime VisitStart(IEnumerable<KeyValuePair<DateTime, EncounterInfo>> entries, DateTime time)
    {
        var preceding = entries.Where(e => e.Key <= time).OrderByDescending(e => e.Key).ToArray();
        if (preceding.Length == 0) return DateTime.MinValue;
        var current = preceding[0].Value;
        var start = preceding[0].Key;
        foreach (var entry in preceding.Skip(1))
        {
            // Repeated same-area records (including reconnect/mode announcements) do not restart
            // a visit. An intervening different area does, whether or not it contained combat.
            if (entry.Value.Name != current.Name || entry.Value.Difficutly != current.Difficutly ||
                entry.Value.NumberOfPlayer != current.NumberOfPlayer)
                break;
            start = entry.Key;
        }
        return start;
    }
}
