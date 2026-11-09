using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public abstract class OverviewViewModel : ReactiveObject
    {
        protected Combat _currentCombat;
        private ObservableCollection<Entity> _availableParticipants = new ObservableCollection<Entity>();
        private Entity selectedEntity;

        public OverviewInstanceViewModel DamageVM;
        public OverviewInstanceViewModel HealingVM;
        public OverviewInstanceViewModel DamageTakenVM;
        public OverviewInstanceViewModel HealingReceivedVM;
        public OverviewInstanceViewModel ThreatVM;
        private UserControl _selectedDataTypeContent;
        private int _selectedTabIndex;
        public abstract bool SortOptionVisibility { get; }

        public ObservableCollection<Entity> AvailableParticipants
        {
            get => _availableParticipants;
            set => this.RaiseAndSetIfChanged(ref _availableParticipants, value);
        }



        public OverviewViewModel()
        {
            ParticipantSelectionHandler.SelectionUpdated += UpdateSelectedEntity;
        }

        private void UpdateSelectedEntity(Entity obj)
        {
            SelectedEntity = obj;
        }

        public abstract int SelectedTabIndex { get; set; }

        public UserControl SelectedDataTypeContent
        {
            get => _selectedDataTypeContent;
            set => this.RaiseAndSetIfChanged(ref _selectedDataTypeContent, value);
        }
        public Entity SelectedEntity
        {
            get => selectedEntity; set
            {
                if(value == null)
                    return;
                var participant = AvailableParticipants.FirstOrDefault(e => e.LogId == value.LogId);
                if (participant == null) return;
                this.RaiseAndSetIfChanged(ref selectedEntity, participant);
                if (selectedEntity == null)
                    return;
                ParticipantSelectionHandler.UpdateSelection(SelectedEntity);
                RefreshData();
            }
        }

        protected virtual void RefreshData()
        {
            if (_currentCombat == null || selectedEntity == null) return;
            DamageVM.UpdateEntity(selectedEntity);
            HealingVM.UpdateEntity(selectedEntity);
            DamageTakenVM.UpdateEntity(selectedEntity);
            HealingReceivedVM.UpdateEntity(selectedEntity);
            ThreatVM.UpdateEntity(selectedEntity);
            DamageVM.UpdateData(_currentCombat);
            DamageTakenVM.UpdateData(_currentCombat);
            ThreatVM.UpdateData(_currentCombat);
            HealingVM.UpdateData(_currentCombat);
            HealingReceivedVM.UpdateData(_currentCombat);
        }
        public void AddCombat(Combat combat)
        {
            if (combat == null || combat.AllEntities.Count == 0) { Reset(); return; }
            _currentCombat = combat;
            UpdateParticipants();
        }
        private void UpdateParticipants()
        {
            if (_currentCombat == null)
            {
                AvailableParticipants = new ObservableCollection<Entity>();
            }
            else
            {
                var participants = _currentCombat.AllEntities.Values.OrderByDescending(e => e.IsCharacter).ThenBy(e => e.Name).ToArray();
                if (!AvailableParticipants.SequenceEqual(participants, System.Collections.Generic.ReferenceEqualityComparer.Instance))
                    AvailableParticipants = new ObservableCollection<Entity>(participants);
                if (SelectedEntity != null && AvailableParticipants.Any(p => p.LogId == SelectedEntity.LogId))
                {
                    SelectedEntity = AvailableParticipants.First(p => p.LogId == SelectedEntity.LogId);
                }
                else if (!AvailableParticipants.Any(p => p.IsLocalPlayer))
                {
                    SelectedEntity = AvailableParticipants.FirstOrDefault();
                }
                else
                {
                    SelectedEntity = AvailableParticipants.First(p => p.IsLocalPlayer);
                }
            }

        }
        public void Reset()
        {
            _currentCombat = null;
            AvailableParticipants = new ObservableCollection<Entity>();
            DamageVM.Reset();
            DamageTakenVM.Reset();
            HealingVM.Reset();
            HealingReceivedVM.Reset();
            ThreatVM.Reset();
        }
    }
}
