using SWTORCombatParser.ViewModels.Overlays.Notes;

namespace SWTORCombatParser.Views.Overlay.Notes
{
    /// <summary>
    /// Interaction logic for RaidNotesView.xaml
    /// </summary>
    public partial class RaidNotesView : BaseOverlayWindow
    {
        public RaidNotesView(RaidNotesViewModel viewModel):base(viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
