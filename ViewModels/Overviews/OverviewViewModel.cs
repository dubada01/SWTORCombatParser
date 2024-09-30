using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public abstract class OverviewViewModel : INotifyPropertyChanged
    {
        private Entity selectedEntity;

        public OverviewInstanceViewModel DamageVM;
        public OverviewInstanceViewModel HealingVM;
        public OverviewInstanceViewModel DamageTakenVM;
        public OverviewInstanceViewModel HealingReceivedVM;
        public OverviewInstanceViewModel ThreatVM;
        public abstract bool SortOptionVisibility { get; }
        public List<Entity> AvailableParticipants { get; set; } = new List<Entity>();
        private Combat _currentCombat;

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
                ThreatVM.UpdateEntity(selectedEntity);
                OnPropertyChanged();

            }
        }
        public void AddCombat(Combat combat)
        {
            _currentCombat = combat;
            UpdateParticipants();
            DamageVM.UpdateData(combat);
            DamageTakenVM.UpdateData(combat);
            ThreatVM.UpdateData(combat);
            HealingVM.UpdateData(combat);
            HealingReceivedVM.UpdateData(combat);
        }
        private void UpdateParticipants()
        {
            if (_currentCombat == null)
            {
                AvailableParticipants = new List<Entity>();
            }
            else
            {
                AvailableParticipants = _currentCombat.AllEntities.Distinct().ToList();
                if (!AvailableParticipants.Any(p => p.IsLocalPlayer))
                {
                    SelectedEntity = AvailableParticipants.FirstOrDefault();
                }
                else
                {
                    if (SelectedEntity != null && AvailableParticipants.Any(p => p.LogId == SelectedEntity.LogId))
                    {
                        SelectedEntity = AvailableParticipants.First(p => p.LogId == SelectedEntity.LogId);
                    }
                    else
                        SelectedEntity = AvailableParticipants.First(p => p.IsLocalPlayer);
                }
                OnPropertyChanged("AvailableParticipants");
            }

        }
        public void Reset()
        {
            _currentCombat = null;
            AvailableParticipants.Clear();
            DamageVM.Reset();
            DamageTakenVM.Reset();
            HealingVM.Reset();
            HealingReceivedVM.Reset();
            ThreatVM.Reset();
            OnPropertyChanged("AvailableParticipants");
        }
    }
}
