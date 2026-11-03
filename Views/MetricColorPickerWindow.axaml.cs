using Avalonia.Controls;
using SWTORCombatParser.ViewModels;

namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for MetricColorPickerWindow.xaml
    /// </summary>
    public partial class MetricColorPickerWindow : Window
    {
        public MetricColorPickerWindow(MetricColorPickerViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
