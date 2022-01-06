using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
