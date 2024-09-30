using Avalonia.Controls;
using Avalonia.Input;
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
            var viewModel = DataContext as PastCombat;
            viewModel.SelectionToggle();
        }
    }
}
