using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.CombatParsing
{
    /// <summary>
    /// Efficiently calculates the average damage avoided while a tank defensive
    /// cooldown is active and annotates the <see cref="Combat"/> instance with
    /// <c>AverageDamageSavedDuringCooldown</c> per target.
    /// </summary>
    public static class TankCooldownProcessor
    {
        // ------------------------------------------------------------------
        // Static data – set of EffectIds that count as a "tank cooldown"
        // ------------------------------------------------------------------
        private static readonly HashSet<ulong> _tankCooldowns = new HashSet<ulong>
        {
            // guardian / juggernaut
            2793700132388864,  812483258351616,   3126160665870336, 812169725739008, 2311847751450624,
            1000001530494976,  3454145843429376,  2312058204848128, 2473484550668288,

            // assassin / shadow
            3421834804461568,  808291370270720,   975430022594560,  954616611078144, 4484405418524672,
            979849543942144,   812822560768000,   4543920780345344,

            // power‑tech / vanguard
            2264298168516608,  986266225082627,   814218425139459,  4325603297722368,
            4504089253642508,  4502478640906240,  3396893929373696, 1003974375243776, 801329228283904
        };

        // ------------------------------------------------------------------
        // Public API – drop‑in replacement for the original implementation
        // ------------------------------------------------------------------
        public static void AddDamageSavedDuringCooldown(Combat combat)
        {
            var state       = CombatLogStateBuilder.CurrentState;
            var damageLogs  = combat.IncomingDamageLogs;               // IReadOnlyDictionary<Entity, List<ParsedLogEntry>>

            // ------------------------------------------------------------------
            // 1) Build a mapping Target → List<CombatModifier> (defensive CDs)
            // ------------------------------------------------------------------
            var cooldownsByTarget = new Dictionary<Entity, List<CombatModifier>>();

            foreach (var effectBag in state.Modifiers.Values)          // effectBag is IDictionary<guid, CombatModifier>
            {
                if (effectBag.Count == 0) continue;

                // quick skip – most bags are not tank cooldowns
                var probe = effectBag.Values.First();
                if (!_tankCooldowns.Contains(probe.EffectId)) continue;

                foreach (var mod in effectBag.Values)
                {
                    if (!_tankCooldowns.Contains(mod.EffectId)) continue; // mixed bag safety

                    if (!cooldownsByTarget.TryGetValue(mod.Target, out var list))
                    {
                        list = new List<CombatModifier>();
                        cooldownsByTarget[mod.Target] = list;
                    }
                    list.Add(mod);
                }
            }

            // ensure chronological order for quick range checks later
            foreach (var list in cooldownsByTarget.Values)
                list.Sort(static (a, b) => a.StartTime.CompareTo(b.StartTime));

            // ------------------------------------------------------------------
            // 2) Scan damage logs per target and accumulate statistics
            // ------------------------------------------------------------------
            foreach (var (target, logs) in damageLogs)
            {
                if (!cooldownsByTarget.TryGetValue(target, out var cds) || cds.Count == 0)
                {
                    combat.AverageDamageSavedDuringCooldown[target] = 0;
                    continue;
                }

                // simple aggregates to avoid per‑hit list allocations
                var buckets = new Dictionary<string, DamageBucket>(StringComparer.Ordinal);

                foreach (var hit in logs)
                {
                    var ability = hit.Ability;
                    if (!buckets.TryGetValue(ability, out var b))
                        b = default;

                    var dmg = hit.Value.MitigatedDblValue;
                    if (IsWithinAnyCooldown(hit.TimeStamp, cds))
                    {
                        b.InsideSum   += dmg;
                        b.InsideCount += 1;
                    }
                    else
                    {
                        b.OutsideSum   += dmg;
                        b.OutsideCount += 1;
                    }
                    buckets[ability] = b;
                }

                // compute final metric: Σ((outsideAvg − insideAvg) · insideCount)
                double totalSaved = 0;
                foreach (var b in buckets.Values)
                {
                    if (b.InsideCount > 2 && b.OutsideCount > 2)
                    {
                        var savedPerHit = Math.Max(0,
                            (b.OutsideSum / b.OutsideCount) - (b.InsideSum / b.InsideCount));
                        totalSaved += savedPerHit * b.InsideCount;
                    }
                }

                combat.AverageDamageSavedDuringCooldown[target] = totalSaved;
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------
        private struct DamageBucket
        {
            public double InsideSum   { get; set; }
            public int    InsideCount { get; set; }
            public double OutsideSum  { get; set; }
            public int    OutsideCount{ get; set; }
        }

        /// <summary>
        /// Returns true when <paramref name="ts"/> falls inside *any* cooldown in <paramref name="cds"/>.
        /// Uses a linear scan; typical list size is small (≤10), so this is faster than building an index.
        /// </summary>
        private static bool IsWithinAnyCooldown(DateTime ts, List<CombatModifier> cds)
        {
            foreach (var cd in cds)
            {
                if (cd.StartTime <= ts && (cd.StopTime > ts || cd.StopTime == DateTime.MinValue))
                    return true;
            }
            return false;
        }
    }
}
