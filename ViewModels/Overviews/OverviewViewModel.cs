﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public abstract class OverviewViewModel : ReactiveObject
    {
        private Combat _currentCombat;
        private List<Entity> _availableParticipants = new List<Entity>();
        private Entity selectedEntity;

        public OverviewInstanceViewModel DamageVM;
        public OverviewInstanceViewModel HealingVM;
        public OverviewInstanceViewModel DamageTakenVM;
        public OverviewInstanceViewModel HealingReceivedVM;
        public OverviewInstanceViewModel ThreatVM;
        private UserControl _selectedDataTypeContent;
        private int _selectedTabIndex;
        public abstract bool SortOptionVisibility { get; }

        public List<Entity> AvailableParticipants
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
                this.RaiseAndSetIfChanged(ref selectedEntity, value);
                if (selectedEntity == null)
                    return;
                ParticipantSelectionHandler.UpdateSelection(SelectedEntity);
                DamageVM.UpdateEntity(selectedEntity);
                HealingVM.UpdateEntity(selectedEntity);
                DamageTakenVM.UpdateEntity(selectedEntity);
                HealingReceivedVM.UpdateEntity(selectedEntity);
                ThreatVM.UpdateEntity(selectedEntity);
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
            }

        }
        public void Reset()
        {
            _currentCombat = null;
            AvailableParticipants = new List<Entity>();
            DamageVM.Reset();
            DamageTakenVM.Reset();
            HealingVM.Reset();
            HealingReceivedVM.Reset();
            ThreatVM.Reset();
        }
    }
}
