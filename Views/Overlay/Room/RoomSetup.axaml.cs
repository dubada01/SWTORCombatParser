using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.Room;
namespace SWTORCombatParser.Views.Overlay.Room
{
    /// <summary>
    /// Interaction logic for RoomSetup.xaml
    /// </summary>
    public partial class RoomSetup : UserControl
    {
        public RoomSetup(RoomOverlayViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
