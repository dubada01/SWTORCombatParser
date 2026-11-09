using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Views.Timers
{
    public partial class TimerSharePopup : Window
    {
        public TimerSharePopup(string id)
        {
            InitializeComponent();
            ShareCode.Text = id;
            OkButton.Click += (_, _) => Close();
            CopyButton.Click += async (_, _) => await CopyCodeAsync();
            Opened += async (_, _) => await CopyCodeAsync();
            KeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape) return;
                e.Handled = true;
                Close();
            };
        }

        private async Task CopyCodeAsync()
        {
            try
            {
                if (Clipboard == null) throw new InvalidOperationException("Clipboard is unavailable.");
                await Clipboard.SetTextAsync(ShareCode.Text ?? "");
                LoadingText.Text = "Copied to clipboard.";
            }
            catch (Exception error)
            {
                Logging.LogError("Could not copy timer share code: " + error.Message);
                LoadingText.Text = "Couldn't copy. Select the code to copy it manually.";
            }
        }
    }
}
