using SWTORCombatParser.ViewModels.Home_View_Models;
using System.Windows.Controls;
using System.Windows.Input;

namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for ParticipantInstanceView.xaml
    /// </summary>
    public partial class ParticipantInstanceView : UserControl
    {
        private ParticipantViewModel _viewModel;
        private bool _isSelected = false;
        public ParticipantInstanceView()
        {
            InitializeComponent();
        }

        private void Border_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewModel = DataContext as ParticipantViewModel;
            _isSelected = !_isSelected;
            _viewModel.ToggleSelection();
        }
    }
}
