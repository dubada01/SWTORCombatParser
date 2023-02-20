using System.Windows.Controls;
using System.Windows.Input;
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
        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as EncounterCombat;

            viewModel.ToggleCombatVisibility();

        }
    }
}
