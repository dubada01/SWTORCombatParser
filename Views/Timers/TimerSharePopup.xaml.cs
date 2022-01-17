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
    public partial class TimerSharePopup : Window
    {
        public TimerSharePopup(string id)
        {
            InitializeComponent();
            this.Left = Application.Current.MainWindow.Left + (Application.Current.MainWindow.ActualWidth / 2) - (750 / 2d);
            this.Top = Application.Current.MainWindow.Top + (Application.Current.MainWindow.ActualHeight / 2) - (450 / 2d);
            OkButton.Click += (e, s) => { Close(); };
            ShareCode.Text = id;
            Clipboard.SetText(id);
        }
    }
}
