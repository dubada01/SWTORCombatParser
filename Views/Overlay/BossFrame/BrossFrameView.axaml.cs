using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.BossFrame;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SWTORCombatParser.Views.Overlay.BossFrame
{
    /// <summary>
    /// Interaction logic for BrossFrameView.xaml
    /// </summary>
    public partial class BrossFrameView : BaseOverlayWindow
    {
        private BossFrameConfigViewModel viewModel;

        public BrossFrameView(BossFrameConfigViewModel vm):base(vm)
        {
            InitializeComponent();
            viewModel = vm;
            DataContext = vm;
        }
    }
}
