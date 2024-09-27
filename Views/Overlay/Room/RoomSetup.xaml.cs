using SWTORCombatParser.ViewModels.Overlays.Room;
using System.Windows.Controls;

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
