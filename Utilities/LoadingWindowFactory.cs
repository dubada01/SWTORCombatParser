using Avalonia.Controls;
using SWTORCombatParser.Views;
using Dispatcher = Avalonia.Threading.Dispatcher;

namespace SWTORCombatParser.Utilities
{
    public static class LoadingWindowFactory
    {
        private static LoadingSplash _loadingWindow;
        private static Window _mainWindow;

        public static bool MainWindowHidden = false;
        public static void SetMainWindow(Window mainWindow)
        {
            _mainWindow = mainWindow;
        }
        public static LoadingSplash ShowLoading(string text = "Loading...")
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                _loadingWindow = new LoadingSplash();
                _loadingWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _loadingWindow.Show(_mainWindow);
            });
            return _loadingWindow;
        }
        public static LoadingSplash ShowInstancedLoading(string text = "Loading...")
        {
            var instancedLoadedSplash = new LoadingSplash();
            Dispatcher.UIThread.Invoke(() =>
            {
                instancedLoadedSplash.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                instancedLoadedSplash.Show(_mainWindow);
            });
            return instancedLoadedSplash;
        }
        public static void HideInstancedLoading(LoadingSplash splash)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                splash.Close();
            });
        }
        public static void ShowBackgroundNotice()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var warning = new BackgroundMonitoringWarning();
                warning.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                warning.Show();
            });
        }
        public static void HideLoading()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                if (_loadingWindow != null)
                    _loadingWindow.Close();
            });
        }
    }
}
