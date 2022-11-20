using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

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
            ExitButton.Click += (e,s) => { ExitApp(); };

            ShowAgainCheck.Checked += CheckChanged;
            ShowAgainCheck.Unchecked += CheckChanged;

            DisableCheck.Checked += DisableCheckChanged;
            DisableCheck.Unchecked += DisableCheckChanged;

            SaveShowAgainChoice();
        }

        private void ExitApp()
        {
            Application.Current.Shutdown();
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
