using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.PvP;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for OpponentHpOverlay.xaml
    /// </summary>
    public partial class OpponentHpOverlay : UserControl
    {
        public OpponentHpOverlay(OpponentOverlayViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
