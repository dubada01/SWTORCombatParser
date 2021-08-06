using SWTORCombatParser.ViewModels.CombatMetaData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class CombatMetaDataViewModel:INotifyPropertyChanged
    {
        private string characterName;

        public string CharacterName { get => characterName; set
            {
                characterName = value;
                OnPropertyChanged();
            } 
        }
        public ObservableCollection<MetaDataInstance> CombatMetaDatas { get; set; } = new ObservableCollection<MetaDataInstance>();

        public event PropertyChangedEventHandler PropertyChanged;

        public void PopulateCombatMetaDatas(Combat combat)
        {
            CombatMetaDatas.Clear();
            var healingMetaData = new MetaDataInstance()
            {
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
                Category = "Damage",
                TotalLabel = "Total: ",
                TotalValue = combat.TotalDamage.ToString("#,##0"),
                MaxLabel = "Max: ",
                MaxValue = combat.MaxDamage.ToString("#,##0"),
                RateLabel = "DPS: ",
                RateValue = combat.DPS.ToString("#,##0.0"),
            };
            CombatMetaDatas.Add(damage);
            CombatMetaDatas.Add(damageTaken);
            CombatMetaDatas.Add(healingMetaData);
            CombatMetaDatas.Add(healingTaken);
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
