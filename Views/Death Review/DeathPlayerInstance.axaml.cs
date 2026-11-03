using Avalonia.Controls;
using Avalonia.Input;
using SWTORCombatParser.ViewModels.Home_View_Models;


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
        private void Border_PreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            _viewModel = DataContext as ParticipantViewModel;
            _isSelected = !_isSelected;
            _viewModel.ToggleSelection();
        }
    }
}
