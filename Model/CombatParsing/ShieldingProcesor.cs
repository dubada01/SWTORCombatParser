using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.AbilityInfo;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser.Model.CombatParsing
{
    public class ShieldingEvent
    {
        public Entity Source;
        public EntityInfo SourceInfo;
        public Entity Target;
        public EntityInfo TargetInfo;
        public string ShieldName;
        public double ShieldValue;
        public DateTime ShieldingTime;
    }
    /// <summary>
    /// Builds synthetic absorb‑shield logs and aggregates shielding statistics.
    /// Drop‑in replacement for the previous implementation; 
    /// dramatically faster on large data sets (hundreds of thousands of log rows).
    /// </summary>
    internal static class ShieldingProcessor
    {
       public static void AddShieldLogsByTarget(
    IReadOnlyDictionary<Entity, List<ParsedLogEntry>> participantShieldLogs,
    Combat combat)
{
    // ---------------------------------------------------------------------
    // 1. Pre-computation look-ups
    // ---------------------------------------------------------------------
    var absorbNames = AbilityLoader.AbsorbAbilities.Values
                                      .Select(a => a.name)
                                      .ToHashSet(StringComparer.Ordinal);

    var state = CombatLogStateBuilder.CurrentState;
    // Historical combats build in parallel against the same modifier history.
    // Consumption belongs to this calculation, never to the shared modifiers.
    var countedAbsorbs = new HashSet<CombatModifier>();

    var modifiersByTarget = state.Modifiers
        .SelectMany(bag => bag.Value)
        .Select(kvp => kvp.Value)
        // Live calculations cannot use an effect removal that has not happened
        // yet. A later removal must not retrospectively introduce an overlapping
        // shield and steal attribution from an earlier completed combat.
        .Where(m => absorbNames.Contains(m.EffectName) &&
                    m.StopTime != DateTime.MinValue && m.StopTime <= combat.EndTime)
        .GroupBy(m => m.Target)
        .ToDictionary(g => g.Key, g => g.ToList());

    var logsByTarget = participantShieldLogs.Values
                                           .SelectMany(l => l)
                                           .GroupBy(l => l.Target);

    // Start with participants, but allow new sources to be added on demand.
    var shieldEventsBySource = participantShieldLogs.Keys
        .ToDictionary(k => k, _ => new List<ShieldingEvent>(32));

    // ---------------------------------------------------------------------
    // 2. Main sweep – mirrors original index-based algorithm
    // ---------------------------------------------------------------------
    foreach (var targetGroup in logsByTarget)
    {
        var target = targetGroup.Key;
        if (!modifiersByTarget.TryGetValue(target, out var mods))
            continue;

        mods.Sort(static (a, b) => a.StartTime.CompareTo(b.StartTime));

        foreach (var log in targetGroup)
        {
            var activeAbsorbs = mods.Where(m => IsModifierActive(m, log, countedAbsorbs))
                                     .OrderBy(a => a.StartTime)
                                     .ToList();
            if (activeAbsorbs.Count == 0) continue;

            for (var i = 0; i < activeAbsorbs.Count; i++)
            {
                var absorb = activeAbsorbs[i];
                var amount = GetAbsorbAmount(log, activeAbsorbs, i, countedAbsorbs);
                if (amount <= 0) continue;

                var source = absorb.Source;

                // 🔒 SAFETY: ensure we have a list for this source.
                if (!shieldEventsBySource.TryGetValue(source, out var list))
                {
                    // Option A: track *all* sources (including NPCs, pets, etc.)
                    list = new List<ShieldingEvent>(32);
                    shieldEventsBySource[source] = list;

                    // If you *only* want participants, use this instead:
                    // if (!participantShieldLogs.ContainsKey(source))
                    //     continue;
                }

                var evt = list.FirstOrDefault(se =>
                    se.ShieldingTime == absorb.StopTime &&
                    se.ShieldName    == absorb.Name    &&
                    se.Target        == target);

                if (evt is null)
                {
                    list.Add(new ShieldingEvent
                    {
                        ShieldName    = absorb.Name,
                        ShieldingTime = absorb.StopTime,
                        ShieldValue   = amount,
                        Source        = source,
                        SourceInfo = log.SourceInfo,
                        Target        = target,
                        TargetInfo = log.TargetInfo
                    });
                }
                else
                {
                    evt.ShieldValue += amount;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // 3. Inject synthetic "Processed Absorb" logs & totals
    // ------------------------------------------------------------------
    // Every pass recomputes the complete derived shielding result. Replace the
    // previous derived records while retaining any physical record that has
    // since taken its key. Keep generated IDs outside the physical reader's
    // nonnegative Int32 line-number range, so later appends cannot overwrite them.
    foreach (var previous in combat.AllLogs.Where(pair => pair.Value.Effect.EffectType == EffectType.AbsorbShield).ToArray())
        combat.AllLogs.TryRemove(previous.Key, out _);
    var nextLineNo = (long)int.MaxValue + 1;

    foreach (var (source, events) in shieldEventsBySource.OrderBy(pair => pair.Key.LogId).ThenBy(pair => pair.Key.Id))
    {
        events.Sort(static (a, b) =>
        {
            var order = a.ShieldingTime.CompareTo(b.ShieldingTime);
            if (order != 0) return order;
            order = string.CompareOrdinal(a.ShieldName, b.ShieldName);
            if (order != 0) return order;
            order = a.Target.LogId.CompareTo(b.Target.LogId);
            return order != 0 ? order : a.Target.Id.CompareTo(b.Target.Id);
        });

        var srcLogs = combat.GetLogsInvolvingEntity(source)
                             .Where(l => l.Effect.EffectType != EffectType.AbsorbShield)
                             .OrderBy(l => l.TimeStamp)
                             .ToList();

        var idx = 0;
        combat.ShieldingProvidedLogs[source]   = new ConcurrentQueue<ParsedLogEntry>();
        combat.TotalProvidedSheilding[source] = 0;

        foreach (var ev in events)
        {
            while (idx < srcLogs.Count && srcLogs[idx].TimeStamp <= ev.ShieldingTime)
                idx++;
            if (idx == srcLogs.Count) break;

            while (combat.AllLogs.ContainsKey(nextLineNo)) nextLineNo++;
            var p = new ParsedLogEntry
            {
                TimeStamp     = ev.ShieldingTime,
                LogLineNumber = nextLineNo++,
                Ability       = ev.ShieldName,
                Effect        = new Effect
                {
                    EffectType = EffectType.AbsorbShield,
                    EffectId   = _7_0LogParsing._healEffectId,
                    EffectName = "Processed Absorb"
                },
                SourceInfo    = ev.SourceInfo,
                TargetInfo    = ev.TargetInfo,
                Value         = new Value
                {
                    EffectiveDblValue = ev.ShieldValue,
                    DisplayValue      = ev.ShieldValue.ToString("N2"),
                    ValueType         = DamageType.heal
                }
            };

            combat.AllLogs[p.LogLineNumber] = p;
            combat.ShieldingProvidedLogs[source].Enqueue(p);
            combat.TotalProvidedSheilding[source] += ev.ShieldValue;
        }
    }

}


        // ------------------------------------------------------------------
        // Original helper logic (ported verbatim except for inlining attribute)
        // ------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsModifierActive(CombatModifier m, ParsedLogEntry log, HashSet<CombatModifier> countedAbsorbs)
        {
            if (countedAbsorbs.Contains(m)) return false;
            return m.StartTime < log.TimeStamp && m.StopTime.AddSeconds(4.25) >= log.TimeStamp;
        }

        private static double GetAbsorbAmount(ParsedLogEntry log, List<CombatModifier> absorbs, int index,
            HashSet<CombatModifier> countedAbsorbs)
        {
            var modVal = log.Value.Modifier.DblValue;

            if (absorbs.Count == 1)
            {
                if (log.Value.MitigatedDblValue > 0)
                    countedAbsorbs.Add(absorbs[index]);
                return modVal;
            }

            if (absorbs.Count > 1)
            {
                if (index > 1 && log.Value.MitigatedDblValue == 0)
                    return 0;

                double firstPortion;
                double remainderPortion = 0d;

                if (Math.Abs(log.Value.DblValue - modVal) < 0.001) // equality within fp tolerance
                {
                    firstPortion = modVal;
                }
                else
                {
                    if (index == 0)
                        countedAbsorbs.Add(absorbs[index]);

                    firstPortion     = (log.Value.DblValue - modVal) - log.Value.MitigatedDblValue;
                    remainderPortion = modVal;
                }

                if (index == 0) return firstPortion;
                if (index == 1) return remainderPortion;
            }
            return 0;
        }
    }
}
