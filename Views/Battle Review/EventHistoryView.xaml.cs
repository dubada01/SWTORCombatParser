using SWTORCombatParser.ViewModels.BattleReview;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Battle_Review
{
    /// <summary>
    /// Interaction logic for EventHistoryView.xaml
    /// </summary>
    public partial class EventHistoryView : UserControl
    {
        public EventHistoryView(EventHistoryViewModel _eventViewModel)
        {
            DataContext = _eventViewModel;
            InitializeComponent();
        }
    }
}
