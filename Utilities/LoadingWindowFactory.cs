using Avalonia;
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
                var mainTop = _mainWindow.Position.Y;
                var mainLeft = _mainWindow.Position.X;
                var mainWidth = _mainWindow.ClientSize.Width;
                var mainHeight = _mainWindow.ClientSize.Width;
                (double, double) center = (mainLeft + (mainWidth / 2), mainTop + (mainHeight / 2));

                _loadingWindow = new LoadingSplash();
                _loadingWindow.Position = new PixelPoint((int)center.Item1 - 150, (int)center.Item2 - 50);
                _loadingWindow.Show();
            });
            return _loadingWindow;
        }
        public static LoadingSplash ShowInstancedLoading(string text = "Loading...")
        {
            var instancedLoadedSplash = new LoadingSplash();
            Dispatcher.UIThread.Invoke(() =>
            {
                var mainTop = _mainWindow.Position.Y;
                var mainLeft = _mainWindow.Position.X;
                var mainWidth = _mainWindow.ClientSize.Width;
                var mainHeight = _mainWindow.ClientSize.Width;
                (double, double) center = (mainLeft + (mainWidth / 2), mainTop + (mainHeight / 2));

                instancedLoadedSplash.Position = new PixelPoint((int)center.Item1 - 150, (int)center.Item2 - 50);
                instancedLoadedSplash.Show();
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
                var mainTop = _mainWindow.Position.Y;
                var mainLeft = _mainWindow.Position.X;
                var mainWidth = _mainWindow.ClientSize.Width;
                var mainHeight = _mainWindow.ClientSize.Width;
                (double, double) center = (mainLeft + (mainWidth / 2), mainTop + (mainHeight / 2));

                var warning = new BackgroundMonitoringWarning();
                warning.Position = new PixelPoint((int)center.Item1 - 300, (int)center.Item2 - 100);
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
