using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;


namespace SWTORCombatParser.Views.Overlay.BossFrame
{
    /// <summary>
    /// Interaction logic for BossFrameView.xaml
    /// </summary>
    public partial class BossFrameView : UserControl
    {
        public BossFrameView(BossFrameConfigViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
