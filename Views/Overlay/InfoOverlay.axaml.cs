using Avalonia.Controls;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for InfoOverlay.xaml
    /// </summary>
    public partial class InfoOverlay : UserControl
    {
        private BaseOverlayViewModel viewModel;
        private bool _hidden;
        public InfoOverlay(BaseOverlayViewModel vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
            HotkeyHandler.OnHideOverlaysHotkey += ToggleHide;
        }
        private void ToggleHide()
        {
            if(_hidden)
            {
                viewModel.ShowOverlayWindow();
            }
            else
            {
                viewModel.HideOverlayWindow();
            }
        }
    }
}
