using SWTORCombatParser.ViewModels.Histogram;
using SWTORCombatParser.Views.Histogram;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.ViewModels
{
    public class HistogramViewModel
    {
        private HistogramInstanceViewModel _damageVm;
        private HistogramInstanceViewModel _damageTakenVm;
        private HistogramInstanceViewModel _healingVm;
        private HistogramInstanceViewModel _healingReceivedVm;
        public HistogramInstanceView DamageContent { get; set; }
        public HistogramInstanceView HealingContent { get; set; }
        public HistogramInstanceView DamageTakenContent { get; set; }
        public HistogramInstanceView HealingReceivedContent { get; set; }
        public List<Entity> AvailableParticipants { get; set; }
        public Entity SelectedEntity
        {
            get => selectedEntity; set
            {
                selectedEntity = value;
                if (selectedEntity != null)
                {
                    _damageVm.UpdateEntity(selectedEntity);
                    _damageTakenVm.UpdateEntity(selectedEntity);
                    _healingVm.UpdateEntity(selectedEntity);
                    _healingReceivedVm.UpdateEntity(selectedEntity);
                }
            }
        }
        public List<Combat> CurrentlySelectedCombats = new List<Combat>();
        private Entity selectedEntity;

        public HistogramViewModel()
        {
            DamageContent = new HistogramInstanceView();
            _damageVm = new HistogramInstanceViewModel(TableDataType.Damage);
            DamageContent.DataContext = _damageVm;

            HealingContent = new HistogramInstanceView();
            _healingVm = new HistogramInstanceViewModel(TableDataType.Healing);
            HealingContent.DataContext = _healingVm;

            DamageTakenContent = new HistogramInstanceView();
            _damageTakenVm = new HistogramInstanceViewModel(TableDataType.DamageTaken);
            DamageTakenContent.DataContext = _damageTakenVm;

            HealingReceivedContent = new HistogramInstanceView();
            _healingReceivedVm = new HistogramInstanceViewModel(TableDataType.HealingReceived);
            HealingReceivedContent.DataContext = _healingReceivedVm;
        }
        public void AddCombat(Combat combatToAdd)
        {
            CurrentlySelectedCombats.Add(combatToAdd);
            UpdateParticipants();
            _damageVm.DisplayNewData(combatToAdd);
            _damageTakenVm.DisplayNewData(combatToAdd);
            _healingVm.DisplayNewData(combatToAdd);
            _healingReceivedVm.DisplayNewData(combatToAdd);
        }
        public void RemoveCombat(Combat combatToRemove)
        {
            CurrentlySelectedCombats.Remove(combatToRemove);
            UpdateParticipants();
            _damageVm.UnselectCombat(combatToRemove);
            _damageTakenVm.UnselectCombat(combatToRemove);
            _healingVm.UnselectCombat(combatToRemove);
            _healingReceivedVm.UnselectCombat(combatToRemove);
        }
        private void UpdateParticipants()
        {
            if (CurrentlySelectedCombats.Count == 0)
            {
                AvailableParticipants = new List<Entity>();
                SelectedEntity = null;
            }
            else
            {
                AvailableParticipants = new List<Entity>(CurrentlySelectedCombats.SelectMany(c => c.CharacterParticipants).Distinct());
                SelectedEntity = AvailableParticipants[0];
            }

        }
    }
}
