using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.CombatMetaData
{
    public static class MetaDataFactory
    {
        public static List<MetaDataInstance> GetPlaceholders()
        {
            return GetMetaDatas(new Combat());
        }
        public static List<MetaDataInstance> GetMetaDatas(Combat combat)
        {
            var metaDatas = new List<MetaDataInstance>();
            var healingMetaData = new MetaDataInstance()
            {
                Color = Brushes.LimeGreen,
                Category = "Healing",
                TotalLabel = "Total: ",
                TotalValue = combat.TotalHealing.ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = combat.MaxHeal.ToString("#,##0"),
                RateLabel = "HPS: ",
                RateValue = combat.HPS.ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E): ",
                EffectiveTotalValue = combat.TotalEffectiveHealing.ToString("#,##0"),
                EffectiveMaxLabel = "Max (E): ",
                EffectiveMaxValue = combat.MaxEffectiveHeal.ToString("#,##0"),
                EffectiveRateLabel = "EHPS: ",
                EffectiveRateValue = combat.EHPS.ToString("#,##0.0"),
            };
            
            var healingTaken = new MetaDataInstance()
            {
                Color = Brushes.CornflowerBlue,
                Category = "Healing Received",
                TotalLabel = "Total: ",
                TotalValue = combat.TotalHealingReceived.ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = combat.MaxIncomingHeal.ToString("#,##0"),
                RateLabel = "HRPS: ",
                RateValue = combat.HTPS.ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E): ",
                EffectiveTotalValue = combat.TotalEffectiveHealingReceived.ToString("#,##0"),
                EffectiveMaxLabel = "Max (E): ",
                EffectiveMaxValue = combat.MaxIncomingEffectiveHeal.ToString("#,##0"),
                EffectiveRateLabel = "EHRPS: ",
                EffectiveRateValue = combat.EHTPS.ToString("#,##0.0"),
            };
            
            var damageTaken = new MetaDataInstance()
            {
                Color = Brushes.Peru,
                Category = "Damage Taken",
                TotalLabel = "Total: ",
                TotalValue = combat.TotalDamageTaken.ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = combat.MaxIncomingDamage.ToString("#,##0"),
                RateLabel = "DTPS: ",
                RateValue = combat.DTPS.ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E): ",
                EffectiveTotalValue = combat.TotalEffectiveDamageTaken.ToString("#,##0"),
                EffectiveMaxLabel = "Max (E): ",
                EffectiveMaxValue = combat.MaxEffectiveIncomingDamage.ToString("#,##0"),
                EffectiveRateLabel = "EDTPS: ",
                EffectiveRateValue = combat.EDTPS.ToString("#,##0.0"),
            };
            
            var damage = new MetaDataInstance()
            {
                Color = Brushes.IndianRed,
                Category = "Damage",
                TotalLabel = "Total: ",
                TotalValue = combat.TotalDamage.ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = combat.MaxDamage.ToString("#,##0"),
                RateLabel = "DPS: ",
                RateValue = combat.DPS.ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E)",
                EffectiveTotalValue = combat.TotalDamage.ToString("#,##0"),
                EffectiveMaxLabel = "Max (E)",
                EffectiveMaxValue = combat.MaxDamage.ToString("#,##0"),
                EffectiveRateLabel = "EDPS",
                EffectiveRateValue = combat.DPS.ToString("#,##0.0"),
            };
            metaDatas.Add(damage);
            metaDatas.Add(damageTaken);
            metaDatas.Add(healingMetaData);
            metaDatas.Add(healingTaken);
            return metaDatas;
        }
    }
}
