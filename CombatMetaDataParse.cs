using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public static class CombatMetaDataParse
    {
        public static void PopulateMetaData(ref Combat combatToPopulate)
        {
            var combatDurationMs = (combatToPopulate.EndTime - combatToPopulate.StartTime).TotalMilliseconds;

            var outgoingLogs = combatToPopulate.Logs.Where(log=>log.Source.IsCharacter).ToList();
            var incomingLogs = combatToPopulate.Logs.Where(log => log.Target.IsCharacter).ToList();

            var damagingLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
            var healingLogs = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

            var damageTakenLogs = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Damage").ToList();
            var healingTakenLogs = incomingLogs.Where(l => l.Effect.EffectType == EffectType.Apply && l.Effect.EffectName == "Heal").ToList();

            var totalHealing = healingLogs.Sum(l => l.Value.DblValue);
            var totalEffectiveHealing = healingLogs.Sum(l => l.Threat * 2.24);

            var totalDamage = damagingLogs.Sum(l => l.Value.DblValue);

            var totalAbilitiesDone = outgoingLogs.Where(l => l.Effect.EffectType == EffectType.Event && l.Effect.EffectName == "AbilityActivate").Count();

            var totalHealingReceived = healingTakenLogs.Sum(l => l.Value.DblValue);
            var totalEffectiveHealingReceived = healingTakenLogs.Sum(l => l.Threat * 2.24);

            var totalDamageTaken = damageTakenLogs.Sum(l => l.Value.DblValue);

            var sheildingLogs = incomingLogs.Where(l => l.Value.Modifier != null && l.Value.Modifier.DamageType == DamageType.shield);

            var totalSheildingDone = sheildingLogs.Count() == 0 ? 0: sheildingLogs.Sum(l => l.Value.Modifier.DblValue);



            combatToPopulate.MaxDamage =damagingLogs.Count == 0 ? 0: damagingLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxHeal = healingLogs.Count == 0 ? 0: healingLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxEffectiveHeal = healingLogs.Count == 0 ? 0 : healingLogs.Max(l => l.Threat*2.24);
            combatToPopulate.TotalDamage = totalDamage;
            combatToPopulate.TotalSheilding = totalSheildingDone;
            combatToPopulate.TotalAbilites = totalAbilitiesDone;
            combatToPopulate.TotalHealing = totalHealing;
            combatToPopulate.TotalEffectiveHealing = totalEffectiveHealing;
            combatToPopulate.TotalDamageTaken = totalDamageTaken;
            combatToPopulate.TotalHealingReceived = totalHealingReceived;
            combatToPopulate.TotalEffectiveHealingReceived = totalEffectiveHealingReceived;
            combatToPopulate.MaxIncomingDamage = damageTakenLogs.Count == 0 ? 0: damageTakenLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxIncomingHeal =healingTakenLogs.Count == 0 ? 0 : healingTakenLogs.Max(l => l.Value.DblValue);
            combatToPopulate.MaxIncomingEffectiveHeal = healingTakenLogs.Count == 0 ? 0 : healingTakenLogs.Max(l => l.Threat * 2.24);


        }
    }
}
