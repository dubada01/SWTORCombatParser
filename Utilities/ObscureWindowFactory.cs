using SWTORCombatParser.Views;
using System.Windows;

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
            _currentWindow.Top = Application.Current.MainWindow.Top;
            _currentWindow.Left = Application.Current.MainWindow.Left + 7;
            _currentWindow.Width = Application.Current.MainWindow.Width - 14;
            _currentWindow.Height = Application.Current.MainWindow.Height - 7;
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
