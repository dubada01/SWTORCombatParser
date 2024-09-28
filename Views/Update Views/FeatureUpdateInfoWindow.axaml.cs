using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SWTORCombatParser.Views
{

    /// <summary>
    /// Interaction logic for FeatureUpdateInfoWindow.xaml
    /// </summary>
    public partial class FeatureUpdateInfoWindow : Window
    {
        public FeatureUpdateInfoWindow()
        {
            InitializeComponent();
        }
        private void Close(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
