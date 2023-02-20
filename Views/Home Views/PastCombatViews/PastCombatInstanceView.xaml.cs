using System.Windows.Controls;
using System.Windows.Input;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.Views.Home_Views.PastCombatViews
{
    /// <summary>
    /// Interaction logic for PastCombatInstanceView.xaml
    /// </summary>
    public partial class PastCombatInstanceView : UserControl
    {
        public PastCombatInstanceView()
        {
            InitializeComponent();
        }

        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as PastCombat;
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                viewModel.AdditiveSelectionToggle();
            }
            else
            {
                viewModel.SelectionToggle();
            }
        }
    }
}
