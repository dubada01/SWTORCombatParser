using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.Notes;

namespace SWTORCombatParser.Views.Overlay.Notes
{
    /// <summary>
    /// Interaction logic for RaidNotesView.xaml
    /// </summary>
    public partial class RaidNotesView : UserControl
    {
        public RaidNotesView(RaidNotesViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
