using SWTORCombatParser.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public abstract class OverviewViewModel:INotifyPropertyChanged
    {
        private Entity selectedEntity;

        public OverviewInstanceViewModel DamageVM;
        public OverviewInstanceViewModel HealingVM;
        public OverviewInstanceViewModel DamageTakenVM;
        public OverviewInstanceViewModel HealingReceivedVM;
        public abstract Visibility SortOptionVisibility { get; }
        public List<Entity> AvailableParticipants { get; set; } = new List<Entity>();
        public List<Combat> CurrentlySelectedCombats = new List<Combat>();

        public event PropertyChangedEventHandler PropertyChanged;
        public OverviewViewModel()
        {
            ParticipantSelectionHandler.SelectionUpdated += UpdateSelectedEntity;
        }

        private void UpdateSelectedEntity(Entity obj)
        {
            SelectedEntity = obj;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public Entity SelectedEntity
        {
            get => selectedEntity; set
            {
                selectedEntity = value;
                if (selectedEntity == null)
                    return;
                ParticipantSelectionHandler.UpdateSelection(SelectedEntity);
                DamageVM.UpdateEntity(selectedEntity);
                HealingVM.UpdateEntity(selectedEntity);
                DamageTakenVM.UpdateEntity(selectedEntity);
                HealingReceivedVM.UpdateEntity(selectedEntity);
                OnPropertyChanged();
                
            }
        }
        public void AddCombat(Combat combat)
        {
            CurrentlySelectedCombats.Add(combat);
            UpdateParticipants();
            DamageVM.UpdateData(combat);
            DamageTakenVM.UpdateData(combat);
            HealingVM.UpdateData(combat);
            HealingReceivedVM.UpdateData(combat);
        }
        public void RemoveCombat(Combat combatToRemove)
        {
            CurrentlySelectedCombats.Remove(combatToRemove);
            UpdateParticipants();
            DamageVM.RemoveData(combatToRemove);
            DamageTakenVM.RemoveData(combatToRemove);
            HealingVM.RemoveData(combatToRemove);
            HealingReceivedVM.RemoveData(combatToRemove);
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
                OnPropertyChanged("AvailableParticipants");
            }

        }
        public void Reset()
        {
            CurrentlySelectedCombats.Clear();
            AvailableParticipants.Clear();
            DamageVM.Reset();
            DamageTakenVM.Reset();
            HealingVM.Reset();
            HealingReceivedVM.Reset();
        }
    }
}
