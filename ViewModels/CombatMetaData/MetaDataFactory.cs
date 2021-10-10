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
            return GetMetaDatas(new Combat(), new Entity());
        }
        public static List<MetaDataInstance> GetMetaDatas(Combat combat, Entity currentParticipant)
        {
            var metaDatas = new List<MetaDataInstance>();
            var healingMetaData = new MetaDataInstance()
            {
                Color = Brushes.LimeGreen,
                Category = "Healing",
                TotalLabel = "Total: ",
                TotalValue = string.IsNullOrEmpty(currentParticipant.Name)?"0": combat.TotalHealing[currentParticipant].ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxHeal[currentParticipant].ToString("#,##0"),
                RateLabel = "HPS: ",
                RateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.HPS[currentParticipant].ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E): ",
                EffectiveTotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalEffectiveHealing[currentParticipant].ToString("#,##0"),
                EffectiveMaxLabel = "Max (E): ",
                EffectiveMaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxEffectiveHeal[currentParticipant].ToString("#,##0"),
                EffectiveRateLabel = "EHPS: ",
                EffectiveRateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.EHPS[currentParticipant].ToString("#,##0.0"),
            };
            
            var healingTaken = new MetaDataInstance()
            {
                Color = Brushes.CornflowerBlue,
                Category = "Healing Received",
                TotalLabel = "Total: ",
                TotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalHealingReceived[currentParticipant].ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxIncomingHeal[currentParticipant].ToString("#,##0"),
                RateLabel = "HRPS: ",
                RateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.HTPS[currentParticipant].ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E): ",
                EffectiveTotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalEffectiveHealingReceived[currentParticipant].ToString("#,##0"),
                EffectiveMaxLabel = "Max (E): ",
                EffectiveMaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxIncomingEffectiveHeal[currentParticipant].ToString("#,##0"),
                EffectiveRateLabel = "EHRPS: ",
                EffectiveRateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.EHTPS[currentParticipant].ToString("#,##0.0"),
            };
            
            var damageTaken = new MetaDataInstance()
            {
                Color = Brushes.Peru,
                Category = "Damage Taken",
                TotalLabel = "Total: ",
                TotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalDamageTaken[currentParticipant].ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxIncomingDamage[currentParticipant].ToString("#,##0"),
                RateLabel = "DTPS: ",
                RateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.DTPS[currentParticipant].ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E): ",
                EffectiveTotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalEffectiveDamageTaken[currentParticipant].ToString("#,##0"),
                EffectiveMaxLabel = "Max (E): ",
                EffectiveMaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxEffectiveIncomingDamage[currentParticipant].ToString("#,##0"),
                EffectiveRateLabel = "EDTPS: ",
                EffectiveRateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.EDTPS[currentParticipant].ToString("#,##0.0"),
            };
            
            var damage = new MetaDataInstance()
            {
                Color = Brushes.IndianRed,
                Category = "Damage",
                TotalLabel = "Total: ",
                TotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalDamage[currentParticipant].ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxDamage[currentParticipant].ToString("#,##0"),
                RateLabel = "DPS: ",
                RateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.DPS[currentParticipant].ToString("#,##0.0"),
                EffectiveTotalLabel = "Total (E)",
                EffectiveTotalValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.TotalEffectiveDamage[currentParticipant].ToString("#,##0"),
                EffectiveMaxLabel = "Max (E)",
                EffectiveMaxValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.MaxEffectiveDamage[currentParticipant].ToString("#,##0"),
                EffectiveRateLabel = "EDPS",
                EffectiveRateValue = string.IsNullOrEmpty(currentParticipant.Name) ? "0" : combat.EDPS[currentParticipant].ToString("#,##0.0"),
            };
            metaDatas.Add(damage);
            metaDatas.Add(damageTaken);
            metaDatas.Add(healingMetaData);
            metaDatas.Add(healingTaken);
            return metaDatas;
        }
    }
}
