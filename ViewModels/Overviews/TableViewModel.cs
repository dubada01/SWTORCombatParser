using SWTORCombatParser.Views.Overviews;
using System.Collections.Generic;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public class TableViewModel : OverviewViewModel
    {
        private SortingOption selectedOrdering;
        private int _selectedTabIndex;

        public TableViewInstance DamageContent { get; set; }
        public TableViewInstance HealingContent { get; set; }
        public TableViewInstance DamageTakenContent { get; set; }
        public TableViewInstance HealingReceivedContent { get; set; }
        public TableViewInstance ThreatContent { get; set; }
        public override bool SortOptionVisibility => true;
        public TableInstanceViewModel ActiveTable => (TableInstanceViewModel)new[] { DamageVM, HealingVM, DamageTakenVM, HealingReceivedVM, ThreatVM }[_selectedTabIndex];
        protected override void RefreshData()
        {
            if (_currentCombat == null || SelectedEntity == null) return;
            var tables = new[] { DamageVM, HealingVM, DamageTakenVM, HealingReceivedVM, ThreatVM };
            for (var i = 0; i < tables.Length; i++)
                ((TableInstanceViewModel)tables[i]).SetContext(_currentCombat, SelectedEntity, selectedOrdering, i == _selectedTabIndex);
        }

        public override int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (value < 0 || value > 4) return;
                this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
                if (_selectedTabIndex == 0)
                    SelectedDataTypeContent = DamageContent;
                else if (_selectedTabIndex == 1)
                    SelectedDataTypeContent = HealingContent;
                else if (_selectedTabIndex == 2)
                    SelectedDataTypeContent = DamageTakenContent;
                else if (_selectedTabIndex == 3)
                    SelectedDataTypeContent = HealingReceivedContent;
                else if (_selectedTabIndex == 4)
                    SelectedDataTypeContent = ThreatContent;
                this.RaisePropertyChanged(nameof(ActiveTable));
                RefreshData();
            }
        }

        public List<SortingOption> AvailableOrderings { get; set; } = new List<SortingOption> { SortingOption.BySource, SortingOption.ByTarget, SortingOption.ByAbility };

        public SortingOption SelectedOrdering
        {
            get => selectedOrdering; set
            {
                if (selectedOrdering == value) return;
                this.RaiseAndSetIfChanged(ref selectedOrdering, value);
                RefreshData();
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

            ThreatContent = new TableViewInstance();
            ThreatVM = new TableInstanceViewModel(OverviewDataType.Threat);
            ThreatContent.DataContext = ThreatVM;

            SelectedDataTypeContent = DamageContent;
        }

    }
}
