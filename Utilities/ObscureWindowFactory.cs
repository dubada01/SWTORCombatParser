using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.Utilities
{
    public static class ObscureWindowFactory
    {
        private static ObscuringWindow _currentWindow;
        public static void ShowObscureWindow()
        {
            if (_currentWindow != null)
                _currentWindow.Close();
            _currentWindow = new ObscuringWindow();
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                _currentWindow.Position = new PixelPoint(mainWindow.Position.X + 7, mainWindow.Position.Y);
                _currentWindow.Width = mainWindow.Width - 14;
                _currentWindow.Height = mainWindow.Height - 7;
            }
            _currentWindow.Show();
        }
        public static void CloseObscureWindow()
        {
            if (_currentWindow == null)
                return;
            _currentWindow.Close();
            _currentWindow = null;
        }
    }
}
