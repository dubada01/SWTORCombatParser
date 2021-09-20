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
        private SortingOption selectedOrdering;

        public TableViewInstance DamageTableContent { get; set; }
        public TableViewInstance HealingTableContent { get; set; }
        public TableViewInstance DamageTakenTableContent { get; set; }
        public List<SortingOption> AvailableOrderings { get; set; } = new List<SortingOption> { SortingOption.BySource, SortingOption.ByTarget, SortingOption.ByAbility };
        public SortingOption SelectedOrdering { get => selectedOrdering; set 
            {
                selectedOrdering = value;
                DamageVM.SortingOption = selectedOrdering;
                DamageTakenVM.SortingOption = selectedOrdering;
                HealingVM.SortingOption = selectedOrdering;
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
        }
        public void AddCombatLogs(Combat combat)
        {
            DamageVM.DisplayNewData(combat);
            DamageTakenVM.DisplayNewData(combat);
            HealingVM.DisplayNewData(combat);
        }
        public void Reset()
        {
            DamageVM.Reset();
            DamageTakenVM.Reset();
            HealingVM.Reset();
        }
    }
}
