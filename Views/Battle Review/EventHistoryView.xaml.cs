using SWTORCombatParser.ViewModels.BattleReview;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Battle_Review
{
    /// <summary>
    /// Interaction logic for EventHistoryView.xaml
    /// </summary>
    public partial class EventHistoryView : UserControl
    {
        EventHistoryViewModel _eventViewModel;
        public EventHistoryView(EventHistoryViewModel eventViewModel)
        {
            DataContext = eventViewModel;
            _eventViewModel= eventViewModel;
            InitializeComponent();
        }
        private void Selection1List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(DataArea.SelectedItem != null)
                DataArea.ScrollIntoView(DataArea.SelectedItem);
        }

        private void DataArea_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _eventViewModel.HasFocus = true;
        }

        private void DataArea_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _eventViewModel.HasFocus = false;
        }
    }
}
