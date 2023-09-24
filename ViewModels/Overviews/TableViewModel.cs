using SWTORCombatParser.Views.Overviews;
using System.Collections.Generic;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public class TableViewModel : OverviewViewModel
    {
        private SortingOption selectedOrdering;

        public TableViewInstance DamageContent { get; set; }
        public TableViewInstance HealingContent { get; set; }
        public TableViewInstance DamageTakenContent { get; set; }
        public TableViewInstance HealingReceivedContent { get; set; }
        public override Visibility SortOptionVisibility => Visibility.Visible;
        public List<SortingOption> AvailableOrderings { get; set; } = new List<SortingOption> { SortingOption.BySource, SortingOption.ByTarget, SortingOption.ByAbility };

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
            DamageContent = new TableViewInstance();
            DamageVM = new TableInstanceViewModel(OverviewDataType.Damage);
            DamageContent.DataContext = DamageVM;

            HealingContent = new TableViewInstance();
            HealingVM = new TableInstanceViewModel(OverviewDataType.Healing);
            HealingContent.DataContext = HealingVM;

            DamageTakenContent = new TableViewInstance();
            DamageTakenVM = new TableInstanceViewModel(OverviewDataType.DamageTaken);
            DamageTakenContent.DataContext = DamageTakenVM;

            HealingReceivedContent = new TableViewInstance();
            HealingReceivedVM = new TableInstanceViewModel(OverviewDataType.HealingReceived);
            HealingReceivedContent.DataContext = HealingReceivedVM;
        }

    }
}
