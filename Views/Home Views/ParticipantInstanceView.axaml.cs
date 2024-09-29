using Avalonia.Controls;
using Avalonia.Input;
using SWTORCombatParser.ViewModels.Home_View_Models;


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

        private void Border_PreviewMouseDown(object sender, PointerPressedEventArgs e)
        {
            _viewModel = DataContext as ParticipantViewModel;
            _isSelected = !_isSelected;
            _viewModel.ToggleSelection();
        }
    }
}
