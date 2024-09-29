using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.PvP;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SWTORCombatParser.Views.Overlay.PvP
{
    /// <summary>
    /// Interaction logic for OpponentHpOverlay.xaml
    /// </summary>
    public partial class OpponentHpOverlay : BaseOverlayWindow
    {
        public OpponentHpOverlay(OpponentOverlayViewModel vm):base(vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
