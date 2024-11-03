using Avalonia.Controls;
using Avalonia.Input;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.Views.Home_Views.PastCombatViews
{
    /// <summary>
    /// Interaction logic for EncounterCombat.xaml
    /// </summary>
    public partial class EncounterCombatView : UserControl
    {
        public EncounterCombatView()
        {
            InitializeComponent();
        }
        private void Border_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (point.Properties.IsLeftButtonPressed)
            {
                var viewModel = DataContext as EncounterCombat;

                viewModel.ToggleCombatVisibility();

            }
        }
    }
}
