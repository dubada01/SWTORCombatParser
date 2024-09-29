using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;


namespace SWTORCombatParser.Views.Overlay.BossFrame
{
    /// <summary>
    /// Interaction logic for RaidHOTsSteup.xaml
    /// </summary>
    public partial class BossFrameSetup : UserControl
    {
        public BossFrameSetup(BossFrameConfigViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
