using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class AddTankCooldown
    {
        private static HashSet<ulong> _tankCooldowns = new HashSet<ulong>
        {
            //guaridan
            2793700132388864,
            812483258351616,
            3126160665870336,
            812169725739008,
            2311847751450624,

            //assasin
            3421834804461568,
            808291370270720,
            975430022594560,
            954616611078144,
            4484405418524672,

            //jugg
            1000001530494976,
            3454145843429376,
            2312058204848128,
            2473484550668288,
            

            //shadow
            979849543942144,
            812822560768000,
            4543920780345344,

            //PT
            2264298168516608,
            //coolant is the effect for explosive fuel
            986266225082627,
            //the id for energy shield
            814218425139459,
            4325603297722368,
            //the id for energy yield
            4504089253642508,
            4502478640906240,

            //vanguard
            3396893929373696,
            1003974375243776,
            801329228283904
        };
        public static void AddDamageSavedDuringCooldown(Combat combat)
        {
            var start = TimeUtility.CorrectedTime;
            var state = CombatLogStateBuilder.CurrentState;
            var modifiers = state.Modifiers;

            var damageLogs = combat.IncomingDamageLogs;
            var damageTargets = damageLogs.Keys;

            foreach (var target in damageTargets)
            {
                var damageTakenDuringCooldowns = new Dictionary<string, List<double>>();
                var damageTakenOutsideOfCooldowns = new Dictionary<string, List<double>>();


                var allLogs = damageLogs[target];
                var logsForTarget = damageLogs[target];
                var uniqueAbilities = logsForTarget.Select(d => d.Ability).Distinct();
                var averageDamageFromAbility = uniqueAbilities.ToDictionary(a => a, a => allLogs.Where(l => l.Ability == a).Select(v => v.Value.MitigatedDblValue).Average());
                var cooldownsForTarget = new List<CombatModifier>();

                foreach (var kvp in modifiers)
                {
                    // grab the inner bag of mods
                    var inner = kvp.Value;
                    if (inner.Count == 0)
                        continue;

                    // cache the “first” modifier only once
                    // (Values is ICollection<ModifierType> on a Dictionary or ConcurrentDictionary)
                    var firstMod = inner.Values.FirstOrDefault();
                    if (firstMod == null)
                        continue;

                    // early-out if it’s not a tank cooldown
                    if (!_tankCooldowns.Contains(firstMod.EffectId))
                        continue;

                    // now scan *once* for matching targets
                    foreach (var mod in inner.Values)
                    {
                        if (mod.Target == target)
                            cooldownsForTarget.Add(mod);
                    }
                }
                if (!cooldownsForTarget.Any())
                {
                    combat.AverageDamageSavedDuringCooldown[target] = 0;
                    continue;
                }
                foreach (var ability in logsForTarget)
                {
                    if (cooldownsForTarget.Any(cd => cd.StartTime <= ability.TimeStamp && (cd.StopTime > ability.TimeStamp || cd.StopTime == DateTime.MinValue)))
                    {
                        if (!damageTakenDuringCooldowns.ContainsKey(ability.Ability))
                            damageTakenDuringCooldowns[ability.Ability] = new List<double> { ability.Value.MitigatedDblValue };
                        else
                            damageTakenDuringCooldowns[ability.Ability].Add(ability.Value.MitigatedDblValue);
                    }
                    else
                    {
                        if (!damageTakenOutsideOfCooldowns.ContainsKey(ability.Ability))
                            damageTakenOutsideOfCooldowns[ability.Ability] = new List<double> { ability.Value.MitigatedDblValue };
                        else
                            damageTakenOutsideOfCooldowns[ability.Ability].Add(ability.Value.MitigatedDblValue);
                    }
                }
                var fun = damageTakenDuringCooldowns.ToDictionary(kvp => kvp.Key, kvp => (damageTakenOutsideOfCooldowns.ContainsKey(kvp.Key) ? damageTakenOutsideOfCooldowns[kvp.Key].Count > 2 && kvp.Value.Count > 2 ? Math.Max(0, (damageTakenOutsideOfCooldowns[kvp.Key].Average() - kvp.Value.Average())) : 0 : 0));
                combat.AverageDamageSavedDuringCooldown[target] = damageTakenDuringCooldowns.Select(kvp => (damageTakenOutsideOfCooldowns.ContainsKey(kvp.Key) ? damageTakenOutsideOfCooldowns[kvp.Key].Count > 2 && kvp.Value.Count > 2 ? Math.Max(0, (damageTakenOutsideOfCooldowns[kvp.Key].Average() - kvp.Value.Average())) : 0 : 0) * kvp.Value.Count).Sum();
            }
        }


    }
}
