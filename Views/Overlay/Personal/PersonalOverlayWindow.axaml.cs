using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.Personal;

namespace SWTORCombatParser.Views.Overlay.Personal
{
    /// <summary>
    /// Interaction logic for PersonalOverlayWindow.xaml
    /// </summary>
    public partial class PersonalOverlayWindow : UserControl
    {
        public PersonalOverlayWindow(PersonalOverlayViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
