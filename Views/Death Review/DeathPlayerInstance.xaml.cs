using SWTORCombatParser.ViewModels.Home_View_Models;
using System.Windows.Controls;
using System.Windows.Input;

namespace SWTORCombatParser.Views.Death_Review
{
    /// <summary>
    /// Interaction logic for DeathPlayerInstance.xaml
    /// </summary>
    public partial class DeathPlayerInstance : UserControl
    {
        private ParticipantViewModel _viewModel;
        private bool _isSelected;

        public DeathPlayerInstance()
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
