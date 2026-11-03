using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using SWTORCombatParser.ViewModels;
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
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var point = e.GetCurrentPoint(sender as Control);
                if (point.Properties.IsLeftButtonPressed)
                {
                    var viewModel = DataContext as PastCombat;
                    var mainViewModel = desktop.MainWindow.DataContext as MainWindowViewModel;
                    mainViewModel.ShowDeathReviewForCombat(viewModel.Combat);
                }
            }
        }
    }
}
