using SWTORCombatParser.Views.Overviews;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public class HistogramVeiewModel : OverviewViewModel
    {
        public HistogramInstanceView DamageContent { get; set; }
        public HistogramInstanceView HealingContent { get; set; }
        public HistogramInstanceView DamageTakenContent { get; set; }
        public HistogramInstanceView HealingReceivedContent { get; set; }
        public override Visibility SortOptionVisibility => Visibility.Collapsed;
        public HistogramVeiewModel()
        {
            DamageContent = new HistogramInstanceView();
            DamageVM = new HistogramInstanceViewModel(OverviewDataType.Damage);
            DamageContent.DataContext = DamageVM;

            HealingContent = new HistogramInstanceView();
            HealingVM = new HistogramInstanceViewModel(OverviewDataType.Healing);
            HealingContent.DataContext = HealingVM;

            DamageTakenContent = new HistogramInstanceView();
            DamageTakenVM = new HistogramInstanceViewModel(OverviewDataType.DamageTaken);
            DamageTakenContent.DataContext = DamageTakenVM;

            HealingReceivedContent = new HistogramInstanceView();
            HealingReceivedVM = new HistogramInstanceViewModel(OverviewDataType.HealingReceived);
            HealingReceivedContent.DataContext = HealingReceivedVM;
        }

    }
}
