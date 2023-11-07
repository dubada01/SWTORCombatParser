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
        private static HashSet<string> _tankCooldowns = new HashSet<string>
        {
            //guaridan
            "Focused Defense",
            "Warding Call",
            "Saber Refelect",
            "Saber Ward",
            "Blade Turning",

            //assasin
            "Deflection",
            "Overcharge Saber",
            "Force Shroud",
            "Recklessness",

            //jugg
            "Invincible",

            //shadow
            "Battle Readiness",
            "Resilience",
            "Force Potency",

            //PT
            //coolant is the effect for explosive fuel
            "986266225082627",
            //the id for energy shield
            "814218425139459",
            "Power Yield",
            //the id for energy yield
            "4504089253642508",
            "Thermal Yield",

            //vanguard
            "Riot Gas",
            "Battle Focus",
            "Reactive Shield"
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
                var cooldownsForTarget = modifiers
                    .Where(m => _tankCooldowns.Contains(m.Value.FirstOrDefault().Value?.EffectName) || _tankCooldowns.Contains(m.Value.FirstOrDefault().Value?.EffectId))
                    .SelectMany(kvp => kvp.Value)
                    .Where(mod => mod.Value.Target == target)
                    .Select(kvp => kvp.Value).ToList();
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
