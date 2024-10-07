using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for BackgroundMonitoringWarning.xaml
    /// </summary>
    public partial class BackgroundMonitoringWarning : Window
    {
        public BackgroundMonitoringWarning()
        {
            InitializeComponent();
            OkButton.Click += (e, s) => { Close(); };
            ExitButton.Click += (e, s) => { ExitApp(); };

            ShowAgainCheck.Checked += CheckChanged;
            ShowAgainCheck.Unchecked += CheckChanged;

            DisableCheck.Checked += DisableCheckChanged;
            DisableCheck.Unchecked += DisableCheckChanged;

            SaveShowAgainChoice();
        }

        private void ExitApp()
        {
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
                Environment.Exit(0);
            }
        }

        private void CheckChanged(object sender, RoutedEventArgs e)
        {
            SaveShowAgainChoice();
        }
        private void DisableCheckChanged(object sender, RoutedEventArgs e)
        {
            SaveDisabledChoice();
        }
        private void SaveShowAgainChoice()
        {
            ShouldShowPopup.SaveShouldShowPopup("BackgroundMonitoring", ShowAgainCheck.IsChecked.Value);
        }
        private void SaveDisabledChoice()
        {
            ShouldShowPopup.SaveShouldShowPopup("BackgroundDisabled", DisableCheck.IsChecked.Value);
        }
    }
}
