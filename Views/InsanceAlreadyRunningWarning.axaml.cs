using Avalonia.Controls;
using Avalonia.Interactivity;
using SWTORCombatParser.Utilities;


namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for BackgroundMonitoringWarning.xaml
    /// </summary>
    public partial class InstanceAlreadyRunningWarning : Window
    {
        public InstanceAlreadyRunningWarning()
        {
            InitializeComponent();
            OkButton.Click += (e, s) => { Close(); };
            ShowAgainCheck.Checked += CheckChanged;
            ShowAgainCheck.Unchecked += CheckChanged;
            SaveShowAgainChoice();
        }
        private void CheckChanged(object sender, RoutedEventArgs e)
        {
            SaveShowAgainChoice();
        }

        private void SaveShowAgainChoice()
        {
            ShouldShowPopup.SaveShouldShowPopup("InstanceRunning", ShowAgainCheck.IsChecked.Value);
        }
    }
}
