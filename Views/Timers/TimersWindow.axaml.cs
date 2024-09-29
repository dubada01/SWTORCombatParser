using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using SWTORCombatParser.ViewModels;

namespace SWTORCombatParser.Views.Timers
{
    public partial class TimersWindow : BaseOverlayWindow
    {
        private BaseOverlayViewModel viewModel;
        private string _currentPlayerName;
        public TimersWindow(BaseOverlayViewModel vm):base(vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
        }
    }
}
