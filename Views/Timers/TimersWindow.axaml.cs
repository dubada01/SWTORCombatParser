using Avalonia.Controls;
using SWTORCombatParser.ViewModels;

namespace SWTORCombatParser.Views.Timers
{
    public partial class TimersWindow : UserControl
    {
        public TimersWindow(BaseOverlayViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
