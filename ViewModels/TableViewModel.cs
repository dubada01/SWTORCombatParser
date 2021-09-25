using SWTORCombatParser.Views.TableViews;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace SWTORCombatParser.ViewModels
{
    public class TableViewModel
    {
        public TableInstanceViewModel DamageVM;
        public TableInstanceViewModel HealingVM;
        public TableInstanceViewModel DamageTakenVM;
        public TableInstanceViewModel HealingReceivedVM;
        private SortingOption selectedOrdering;
        private Entity selectedEntity;

        public TableViewInstance DamageTableContent { get; set; }
        public TableViewInstance HealingTableContent { get; set; }
        public TableViewInstance DamageTakenTableContent { get; set; }
        public TableViewInstance HealingReceivedTableContent { get; set; }
        public List<SortingOption> AvailableOrderings { get; set; } = new List<SortingOption> { SortingOption.BySource, SortingOption.ByTarget, SortingOption.ByAbility };
        public List<Entity> AvailableParticipants { get; set; }
        public Entity SelectedEntity
        {
            get => selectedEntity; set
            {
                selectedEntity = value;
                if (selectedEntity == null)
                    return;
                DamageVM.UpdateEntity(selectedEntity);
                HealingVM.UpdateEntity(selectedEntity);
                DamageTakenVM.UpdateEntity(selectedEntity);
                HealingReceivedVM.UpdateEntity(selectedEntity);
            }
        }
        public SortingOption SelectedOrdering
        {
            get => selectedOrdering; set
            {
                selectedOrdering = value;
                DamageVM.SortingOption = selectedOrdering;
                DamageTakenVM.SortingOption = selectedOrdering;
                HealingVM.SortingOption = selectedOrdering;
                HealingReceivedVM.SortingOption = selectedOrdering;
            }
        }
        public TableViewModel()
        {
            DamageTableContent = new TableViewInstance();
            DamageVM = new TableInstanceViewModel(TableDataType.Damage);
            DamageTableContent.DataContext = DamageVM;

            HealingTableContent = new TableViewInstance();
            HealingVM = new TableInstanceViewModel(TableDataType.Healing);
            HealingTableContent.DataContext = HealingVM;

            DamageTakenTableContent = new TableViewInstance();
            DamageTakenVM = new TableInstanceViewModel(TableDataType.DamageTaken);
            DamageTakenTableContent.DataContext = DamageTakenVM;

            HealingReceivedTableContent = new TableViewInstance();
            HealingReceivedVM = new TableInstanceViewModel(TableDataType.HealingReceived);
            HealingReceivedTableContent.DataContext = HealingReceivedVM;
        }
        public void AddCombatLogs(Combat combat)
        {
            AvailableParticipants = new List<Entity>(combat.CharacterParticipants);
            SelectedEntity = AvailableParticipants[0];
            DamageVM.DisplayNewData(combat);
            DamageTakenVM.DisplayNewData(combat);
            HealingVM.DisplayNewData(combat);
            HealingReceivedVM.DisplayNewData(combat);
        }
        public void Reset()
        {
            DamageVM.Reset();
            DamageTakenVM.Reset();
            HealingVM.Reset();
            HealingReceivedVM.Reset();
        }
    }
}
