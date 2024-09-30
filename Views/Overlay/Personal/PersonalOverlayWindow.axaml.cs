using SWTORCombatParser.ViewModels.Overlays.Personal;

namespace SWTORCombatParser.Views.Overlay.Personal
{
    /// <summary>
    /// Interaction logic for PersonalOverlayWindow.xaml
    /// </summary>
    public partial class PersonalOverlayWindow : BaseOverlayWindow
    {
        public PersonalOverlayWindow(PersonalOverlayViewModel vm):base(vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
