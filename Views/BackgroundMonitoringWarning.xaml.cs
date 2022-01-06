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
            ShouldShowPopup.SaveShouldShowPopup("BackgroundMonitoring", ShowAgainCheck.IsChecked.Value);
        }
    }
}
