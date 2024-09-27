using SWTORCombatParser.ViewModels.Overlays;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    public partial class OverlayView : UserControl
    {
        public OverlayView(OverlayViewModel viewmodel)
        {
            DataContext = viewmodel;
            InitializeComponent();
        }
    }
}
