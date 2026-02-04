using SWTORCombatParser.ViewModels.Overlays.PvP;
using Avalonia.Controls;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for MiniMapView.xaml
    /// </summary>
    public partial class MedalTrackingView : UserControl
    {
        public MedalTrackingView(MedalTrackingViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
