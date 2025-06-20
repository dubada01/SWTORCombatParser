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
        public Entity Target;
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
            // 1. Pre‑computation look‑ups
            // ---------------------------------------------------------------------
            var absorbNames = AbilityLoader.AbsorbAbilities.Values
                                              .Select(a => a.name)
                                              .ToHashSet(StringComparer.Ordinal);

            var state = CombatLogStateBuilder.CurrentState;

            var modifiersByTarget = state.Modifiers
                .SelectMany(bag => bag.Value)
                .Select(kvp => kvp.Value)
                .Where(m => absorbNames.Contains(m.EffectName))
                .GroupBy(m => m.Target)
                .ToDictionary(g => g.Key, g => g.ToList());

            var logsByTarget = participantShieldLogs.Values
                                                   .SelectMany(l => l)
                                                   .GroupBy(l => l.Target);

            var shieldEventsBySource = participantShieldLogs.Keys
                                                            .ToDictionary(k => k, _ => new List<ShieldingEvent>(32));

            // ---------------------------------------------------------------------
            // 2. Main sweep – mirrors original index‑based algorithm
            // ---------------------------------------------------------------------
            foreach (var targetGroup in logsByTarget)
            {
                var target = targetGroup.Key;
                if (!modifiersByTarget.TryGetValue(target, out var mods))
                    continue;

                mods.Sort(static (a, b) => a.StartTime.CompareTo(b.StartTime));

                foreach (var log in targetGroup)
                {
                    var activeAbsorbs = mods.Where(m => IsModifierActive(m, log))
                                             .OrderBy(a => a.StartTime)
                                             .ToList();
                    if (activeAbsorbs.Count == 0) continue;

                    for (var i = 0; i < activeAbsorbs.Count; i++)
                    {
                        var absorb = activeAbsorbs[i];
                        var amount = GetAbsorbAmount(log, activeAbsorbs, i);
                        if (amount <= 0) continue;

                        var source = absorb.Source;
                        var list   = shieldEventsBySource[source];

                        var evt = list.FirstOrDefault(se =>
                            se.ShieldingTime == absorb.StopTime &&
                            se.ShieldName    == absorb.Name      &&
                            se.Target        == target);

                        if (evt is null)
                        {
                            list.Add(new ShieldingEvent
                            {
                                ShieldName    = absorb.Name,
                                ShieldingTime = absorb.StopTime,
                                ShieldValue   = amount,
                                Source        = source,
                                Target        = target
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
            var nextLineNo = combat.AllLogs.Count == 0 ? 1 : combat.AllLogs.Keys.Max() + 1;

            foreach (var (source, events) in shieldEventsBySource)
            {
                events.Sort(static (a, b) => a.ShieldingTime.CompareTo(b.ShieldingTime));

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
                        SourceInfo    = new EntityInfo { Entity = ev.Source },
                        TargetInfo    = new EntityInfo { Entity = ev.Target },
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

            // reset flags for next pass
            foreach (var bag in state.Modifiers.Values)
                foreach (var mod in bag.Values)
                    mod.HasAbsorbBeenCounted = false;
        }

        // ------------------------------------------------------------------
        // Original helper logic (ported verbatim except for inlining attribute)
        // ------------------------------------------------------------------

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsModifierActive(CombatModifier m, ParsedLogEntry log)
        {
            if (m.HasAbsorbBeenCounted) return false;
            return m.StartTime < log.TimeStamp && m.StopTime.AddSeconds(4.25) >= log.TimeStamp;
        }

        private static double GetAbsorbAmount(ParsedLogEntry log, List<CombatModifier> absorbs, int index)
        {
            var modVal = log.Value.Modifier.DblValue;

            if (absorbs.Count == 1)
            {
                if (log.Value.MitigatedDblValue > 0)
                    absorbs[index].HasAbsorbBeenCounted = true;
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
                        absorbs[index].HasAbsorbBeenCounted = true;

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
