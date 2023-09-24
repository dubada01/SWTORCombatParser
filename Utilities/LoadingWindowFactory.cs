using SWTORCombatParser.Views;
using System.Windows;

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
            App.Current.Dispatcher.Invoke(() =>
            {
                var mainTop = _mainWindow.Top;
                var mainLeft = _mainWindow.Left;
                var mainWidth = _mainWindow.ActualWidth;
                var mainHeight = _mainWindow.ActualHeight;
                (double, double) center = (mainLeft + (mainWidth / 2), mainTop + (mainHeight / 2));

                _loadingWindow = new LoadingSplash();
                _loadingWindow.Top = center.Item2 - 50;
                _loadingWindow.Left = center.Item1 - 150;
                _loadingWindow.Show();
            });
            return _loadingWindow;
        }
        public static LoadingSplash ShowInstancedLoading(string text = "Loading...")
        {
            var instancedLoadedSplash = new LoadingSplash();
            App.Current.Dispatcher.Invoke(() =>
            {
                var mainTop = _mainWindow.Top;
                var mainLeft = _mainWindow.Left;
                var mainWidth = _mainWindow.ActualWidth;
                var mainHeight = _mainWindow.ActualHeight;
                (double, double) center = (mainLeft + (mainWidth / 2), mainTop + (mainHeight / 2));

                instancedLoadedSplash.Top = center.Item2 - 50;
                instancedLoadedSplash.Left = center.Item1 - 150;
                instancedLoadedSplash.Show();
            });
            return instancedLoadedSplash;
        }
        public static void HideInstancedLoading(LoadingSplash splash)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                splash.Close();
            });
        }
        public static void ShowBackgroundNotice()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                var mainTop = _mainWindow.Top;
                var mainLeft = _mainWindow.Left;
                var mainWidth = _mainWindow.ActualWidth;
                var mainHeight = _mainWindow.ActualHeight;
                (double, double) center = (mainLeft + (mainWidth / 2), mainTop + (mainHeight / 2));

                var warning = new BackgroundMonitoringWarning();
                warning.Top = center.Item2 - 100;
                warning.Left = center.Item1 - 300;
                warning.Show();
            });
        }
        public static void HideLoading()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                if (_loadingWindow != null)
                    _loadingWindow.Close();
            });
        }
    }
}
