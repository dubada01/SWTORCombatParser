using SWTORCombatParser.ViewModels.Overlays.PvP;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for OpponentHpOverlay.xaml
    /// </summary>
    public partial class OpponentHpOverlay : BaseOverlayWindow
    {
        public OpponentHpOverlay(OpponentOverlayViewModel vm):base(vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
