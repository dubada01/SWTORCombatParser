using System.Windows;

namespace SWTORCombatParser.Views.Timers
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
