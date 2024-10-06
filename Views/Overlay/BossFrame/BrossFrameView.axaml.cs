using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;


namespace SWTORCombatParser.Views.Overlay.BossFrame
{
    /// <summary>
    /// Interaction logic for BrossFrameView.xaml
    /// </summary>
    public partial class BrossFrameView : UserControl
    {
        public BrossFrameView(BossFrameConfigViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}
