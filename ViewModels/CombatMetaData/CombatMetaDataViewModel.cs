using ScottPlot;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.CombatMetaData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class CombatMetaDataViewModel : INotifyPropertyChanged
    {
        private Entity characterName = new Entity();
        private Combat _currentCombat;
        private List<CombatModifier> _currentCombatModifiers;
        private EffectViewModel selectedEffect;
        private List<Entity> availableParticipants = new List<Entity>();

        public event Action<Entity> OnNewParticipantSelected = delegate { };
        public List<Entity> AvailableParticipants { get => availableParticipants; set { 
                availableParticipants = value;
                SelectedParticipant = availableParticipants[0];
                OnPropertyChanged();
            } }
        public Entity SelectedParticipant
        {
            get => characterName; set
            {
                characterName = value;
                OnNewParticipantSelected(SelectedParticipant);
                OnPropertyChanged();
            }
        }
        public ICommand ClearCombatEffectsCommand => new CommandHandler(ClearCombatEffects);

        private void ClearCombatEffects()
        {
            foreach (var effect in CombatEffects)
            {
                effect.Selected = false;
            }
            SelectedEffect = null;
            OnEffectsCleared();
        }

        public ObservableCollection<MetaDataInstance> CombatMetaDatas { get; set; } = new ObservableCollection<MetaDataInstance>();
        public ObservableCollection<EffectViewModel> CombatEffects { get; set; } = new ObservableCollection<EffectViewModel>();
        public event Action<List<CombatModifier>> OnEffectSelected = delegate { };
        public event Action OnEffectsCleared = delegate { };
        public EffectViewModel SelectedEffect
        {
            get => selectedEffect; set
            {
                selectedEffect = value;
                if (selectedEffect == null)
                    return;
                foreach (var effect in CombatEffects)
                {
                    effect.Selected = false;
                }
                selectedEffect.Selected = true;
                OnEffectSelected(_currentCombatModifiers.Where(m => m.Name == selectedEffect.Name).ToList());
            }
        }



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
            var metaDatas = MetaDataFactory.GetMetaDatas(combat, SelectedParticipant);
            foreach (var metaData in metaDatas)
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
            if (string.IsNullOrEmpty(SelectedParticipant.Name))
                return;
            var combatLogsInView = combatLogs[SelectedParticipant].Where(l => (l.TimeStamp - startTime).TotalSeconds >= minX && (l.TimeStamp - startTime).TotalSeconds <= maxX);

            if (combatLogsInView.Count() == 0)
            {
                CombatMetaDatas.Clear();
                CombatEffects.Clear();
                return;
            }
            var newCombat = CombatIdentifier.GenerateNewCombatFromLogs(combatLogsInView.ToList());
            UpdateMetaDataFromCombat(newCombat);
            var currentState = CombatLogStateBuilder.CurrentStates[newCombat.LogFileName];
            var modifiersDuringCombat = currentState.GetCombatModifiersBetweenTimes(newCombat.StartTime, newCombat.EndTime);
            var abilities = modifiersDuringCombat.Select(m => m.Name).Distinct();
            var durations = modifiersDuringCombat.GroupBy(v => (v.Name, v.Source),
                v => Math.Min(v.DurationSeconds, (newCombat.EndTime - v.StartTime).TotalSeconds), (info, durations) =>
                new EffectViewModel()
                {
                    Name = info.Name,
                    Source = info.Source.Name,
                    Duration = durations.Sum(),
                    Count = durations.Count()
                }).OrderByDescending(effect => effect.Duration).ToList();
            CombatEffects.Clear();
            durations.ForEach(ef => CombatEffects.Add(ef));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
