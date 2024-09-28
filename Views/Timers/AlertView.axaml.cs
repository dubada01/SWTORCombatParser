using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace SWTORCombatParser.Views.Timers
{
    /// <summary>
    /// Interaction logic for AlertView.xaml
    /// </summary>
    public partial class AlertView : BaseOverlayWindow
    {
        public AlertView(AlertsWindowViewModel vm):base(vm)
        {
            InitializeComponent();
        }
        public void SetIdText(string text)
        {

        }
    }
}
