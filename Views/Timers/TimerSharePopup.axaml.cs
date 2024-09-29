using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

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
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Position = new PixelPoint((int)(desktop.MainWindow.Position.X + (desktop.MainWindow.Width / 2) - (750 / 2)), (int)(desktop.MainWindow.Position.Y + (desktop.MainWindow.Height / 2) - (450 / 2)));
            }
            OkButton.Click += (e, s) => { Close(); };
            ShareCode.Text = id;
            Clipboard.SetTextAsync(id);
        }
    }
}
