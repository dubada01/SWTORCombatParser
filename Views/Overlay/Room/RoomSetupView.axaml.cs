using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.Room;
namespace SWTORCombatParser.Views.Overlay.Room
{
    /// <summary>
    /// Interaction logic for RoomSetupView.xaml
    /// </summary>
    public partial class RoomSetupView : UserControl
    {
        public RoomSetupView(RoomOverlayViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
