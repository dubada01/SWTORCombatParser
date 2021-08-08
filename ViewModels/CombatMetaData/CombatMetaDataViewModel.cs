using ScottPlot;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.CombatMetaData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class CombatMetaDataViewModel : INotifyPropertyChanged
    {
        private string characterName;
        private Combat _currentCombat;
        private List<CombatModifier> _currentCombatModifiers;
        private EffectViewModel selectedEffect;

        public string CharacterName
        {
            get => characterName; set
            {
                characterName = value;
                OnPropertyChanged();
            }
        }
        public ObservableCollection<MetaDataInstance> CombatMetaDatas { get; set; } = new ObservableCollection<MetaDataInstance>();
        public ObservableCollection<EffectViewModel> CombatEffects { get; set; } = new ObservableCollection<EffectViewModel>();
        public event Action<List<CombatModifier>> OnEffectSelected = delegate { };
        public EffectViewModel SelectedEffect { get => selectedEffect; set { 
                selectedEffect = value;
                if (selectedEffect == null)
                    return;
                OnEffectSelected(_currentCombatModifiers.Where(m => m.Name == selectedEffect.Name).ToList());
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void PopulateCombatMetaDatas(Combat combat)
        {
            _currentCombat = combat;
            var currentState = CombatLogParser.GetCurrentLogState();
            _currentCombatModifiers = currentState.GetCombatModifiersBetweenTimes(_currentCombat.StartTime, _currentCombat.EndTime);
            UpdateMetaDataFromCombat(_currentCombat);

        }
        private void UpdateMetaDataFromCombat(Combat combat)
        {
            CombatMetaDatas.Clear();
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
            CombatMetaDatas.Add(damage);
            CombatMetaDatas.Add(damageTaken);
            CombatMetaDatas.Add(healingMetaData);
            CombatMetaDatas.Add(healingTaken);
        }
        internal void UpdateBasedOnVisibleData(AxisLimits newAxisLimits)
        {
            if (_currentCombat == null)
                return;
            var minX = newAxisLimits.XMin;
            var maxX = newAxisLimits.XMax;
            var combatLogs = _currentCombat.Logs;

            var startTime = _currentCombat.StartTime;

            var combatLogsInView = combatLogs.Where(l => (l.TimeStamp - startTime).TotalSeconds >= minX && (l.TimeStamp - startTime).TotalSeconds <= maxX);

            if (combatLogsInView.Count() == 0)
            {
                CombatMetaDatas.Clear();
                CombatEffects.Clear();
                return;
            }
            var newCombat = CombatIdentifier.ParseOngoingCombat(combatLogsInView.ToList());
            UpdateMetaDataFromCombat(newCombat);
            var currentState = CombatLogParser.GetCurrentLogState();
            var modifiersDuringCombat = currentState.GetCombatModifiersBetweenTimes(newCombat.StartTime, newCombat.EndTime);
            var abilities = modifiersDuringCombat.Select(m => m.Name).Distinct();
            var durations = modifiersDuringCombat.GroupBy(v => (v.Name,v.Source), v => Math.Min(v.DurationSeconds, (newCombat.EndTime - v.StartTime).TotalSeconds), (info, durations) => new EffectViewModel (){ Name =info.Name,Source = info.Source, Duration = durations.Sum(), Count = durations.Count() }).OrderByDescending(effect => effect.Duration).ToList();
            CombatEffects.Clear();
            durations.ForEach(ef => CombatEffects.Add(ef));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
