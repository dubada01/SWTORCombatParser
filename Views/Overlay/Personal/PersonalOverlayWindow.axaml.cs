using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.Personal;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

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
