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
        public void UpdateMetaDataFromCombat(Combat combat)
        {
            CombatMetaDatas.Clear();
            var metaDatas = MetaDataFactory.GetMetaDatas(combat);
            foreach(var metaData in metaDatas)
            {
                CombatMetaDatas.Add(metaData);
            }
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
            var durations = modifiersDuringCombat.GroupBy(v => (v.Name,v.Source), v => Math.Min(v.DurationSeconds, (newCombat.EndTime - v.StartTime).TotalSeconds), (info, durations) => new EffectViewModel (){ Name =info.Name,Source = info.Source.Name, Duration = durations.Sum(), Count = durations.Count() }).OrderByDescending(effect => effect.Duration).ToList();
            CombatEffects.Clear();
            durations.ForEach(ef => CombatEffects.Add(ef));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
