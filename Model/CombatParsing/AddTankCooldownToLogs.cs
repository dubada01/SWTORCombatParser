using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class AddTankCooldown
    {
        private static List<string> _tankCooldowns = new List<string>
        {
            "Focused Defense",
            "Warding Call",
            "Saber Refelect",
            "Saber Ward",
            "Blade Turning",
            //assasin tank
            "Deflection",
            "Overcharge Saber",
            "Force Shroud",
            "Recklessness",
            //jugg tank
            "Invincible",
            //shadow tank
            "Battle Readiness",
            "Resilience",
            "Force Potency",
            //PT tank
            "Oil Slick",
            "Explosive Fuel",
            "Energy Shield",
            "Power Yield",
            "Energy Yield",
            "Thermal Yield",
            //vanguard tank
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
            
            foreach(var target in damageTargets)
            {
                var damageTakenDuringCooldowns = new Dictionary<string,List<double>>();
                var damageTakenOutsideOfCooldowns = new Dictionary<string, List<double>>();


                var allLogs = damageLogs[target];
                var logsForTarget = damageLogs[target];
                var uniqueAbilities = logsForTarget.Select(d => d.Ability).Distinct();
                var averageDamageFromAbility = uniqueAbilities.ToDictionary(a => a, a => allLogs.Where(l => l.Ability == a).Select(v => v.Value.EffectiveDblValue).Average());
                var cooldownsForTarget = modifiers
                    .Where(m => _tankCooldowns.Contains(m.Value.First().Value.EffectName))
                    .SelectMany(kvp => kvp.Value)
                    .Where(mod => mod.Value.Target == target)
                    .Select(kvp => kvp.Value).ToList();
                if (!cooldownsForTarget.Any())
                {
                    combat.AverageDamageSavedDuringCooldown[target] = 0;
                    continue;
                }
                foreach(var ability in logsForTarget)
                {
                    if(cooldownsForTarget.Any(cd => cd.StartTime <= ability.TimeStamp && (cd.StopTime > ability.TimeStamp || cd.StopTime == DateTime.MinValue)))
                    {
                        if (!damageTakenDuringCooldowns.ContainsKey(ability.Ability))
                            damageTakenDuringCooldowns[ability.Ability] = new List<double> { ability.Value.EffectiveDblValue };
                        else
                            damageTakenDuringCooldowns[ability.Ability].Add(ability.Value.EffectiveDblValue);
                    }
                    else
                    {
                        if (!damageTakenOutsideOfCooldowns.ContainsKey(ability.Ability))
                            damageTakenOutsideOfCooldowns[ability.Ability] = new List<double> { ability.Value.EffectiveDblValue };
                        else
                            damageTakenOutsideOfCooldowns[ability.Ability].Add(ability.Value.EffectiveDblValue);
                    }
                }
                var fun = damageTakenDuringCooldowns.ToDictionary(kvp => kvp.Key, kvp => (damageTakenOutsideOfCooldowns.ContainsKey(kvp.Key) ? damageTakenOutsideOfCooldowns[kvp.Key].Count > 2 && kvp.Value.Count >2 ? Math.Max(0, (damageTakenOutsideOfCooldowns[kvp.Key].Average() - kvp.Value.Average())) :0: 0));
                combat.AverageDamageSavedDuringCooldown[target] = damageTakenDuringCooldowns.Select(kvp => (damageTakenOutsideOfCooldowns.ContainsKey(kvp.Key) ? damageTakenOutsideOfCooldowns[kvp.Key].Count > 2 && kvp.Value.Count > 2 ? Math.Max(0,(damageTakenOutsideOfCooldowns[kvp.Key].Average() - kvp.Value.Average())) : 0 : 0) * kvp.Value.Count).Sum();
            }
        }

       
    }
}
