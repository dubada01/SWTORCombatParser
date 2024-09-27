using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Overlays;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for OverlayOptionView.xaml
    /// </summary>
    public partial class OverlayOptionView : UserControl
    {
        public OverlayOptionView()
        {
            InitializeComponent();
        }

        private void Button_PreviewMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var metricViewModel = (OverlayOptionViewModel)DataContext;
            var viewModel = new MetricColorPickerViewModel(metricViewModel.Type);

            var view = new MetricColorPickerWindow(viewModel);
            viewModel.CloseRequested += () => {
                view.Close();
            };

            view.Owner = App.Current.MainWindow;
            view.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            view.ShowDialog();
        }
    }
}
