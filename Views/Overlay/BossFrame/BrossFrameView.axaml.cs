using SWTORCombatParser.ViewModels.Overlays.BossFrame;


namespace SWTORCombatParser.Views.Overlay.BossFrame
{
    /// <summary>
    /// Interaction logic for BrossFrameView.xaml
    /// </summary>
    public partial class BrossFrameView : BaseOverlayWindow
    {
        private BossFrameConfigViewModel viewModel;

        public BrossFrameView(BossFrameConfigViewModel vm):base(vm)
        {
            InitializeComponent();
            viewModel = vm;
            DataContext = vm;
        }
    }
}
