using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using System.Windows.Input;

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

        private void Border_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (point.Properties.IsLeftButtonPressed)
            {
                var viewModel = DataContext as PastCombat;
                viewModel.SelectionToggle();
            }
        }
        private void DeathReviewBorder_PreviewMouseLeftButtonDown(object sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(sender as Control);
            if (point.Properties.IsLeftButtonPressed && DataContext is PastCombat viewModel)
            {
                e.Handled = true;
                ((ICommand)viewModel.DisplayWipeReviewCommand).Execute(null);
            }
        }
    }
}
