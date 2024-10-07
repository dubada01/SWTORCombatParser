using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays;

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    public partial class OverlayView : Window
    {
        public OverlayView(OverlayViewModel viewmodel)
        {
            DataContext = viewmodel;
            InitializeComponent();
        }
    }
}
