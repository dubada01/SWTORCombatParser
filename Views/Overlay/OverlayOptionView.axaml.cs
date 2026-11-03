using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Overlays;

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

        private void Button_PreviewMouseRightButtonUp(object sender, PointerReleasedEventArgs e)
        {
            var metricViewModel = (OverlayOptionViewModel)DataContext;
            var viewModel = new MetricColorPickerViewModel(metricViewModel.Type);

            var view = new MetricColorPickerWindow(viewModel);
            viewModel.CloseRequested += () => {
                view.Close();
            };
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                view.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                view.ShowDialog(desktop.MainWindow);
            }
        }
    }
}
