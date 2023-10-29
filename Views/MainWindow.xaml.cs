using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;

namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public static class MainWindowClosing
    {
        public static event Action Closing = delegate { };
        public static event Action Hiding = delegate { };
        public static void FireClosing()
        {
            Closing();
        }
        public static void FireHidden()
        {
            Hiding();
        }
    }
    public static class HeaderSelectionState
    {
        public static event Action NewHeaderSelected = delegate { };
        public static string CurrentlySelectedTabHeader = "";
        public static void UpdateSelectedHeader(string header)
        {
            CurrentlySelectedTabHeader = header;
            NewHeaderSelected();
        }
    }
    public partial class MainWindow : Window
    {
        // Constants used for the Windows API
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_ALT = 0x0001;
        private const int WM_HOTKEY = 0x0312;

        // RegisterHotKey and UnregisterHotKey methods from Windows API
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private NotifyIcon _notifyIcon1;
        private bool _actuallyClosing = false;
        public MainWindow()
        {
            InitializeComponent();
            LoadingWindowFactory.SetMainWindow(this);
            var windowInfo = OrbsWindowManager.GetWindowSizeAndPosition();
            Top = windowInfo.TopLeft.Y;
            Left = windowInfo.TopLeft.X;
            Width = windowInfo.Width;
            Height = windowInfo.Height;
            AddNotificationIcon();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //register lock hotkey for CTRL+ALT+L
            RegisterHotKey(1, 0x4c);
            //register refresh hotkey for CTRL+ALT+R
            RegisterHotKey(2, 0x52);
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Unregister the hotkey when the window is closed to clean up
            UnregisterHotKey(1);
            UnregisterHotKey(2);
        }
        private void AddNotificationIcon()
        {
            var components = new Container();
            var contextMenu1 = new ContextMenuStrip();

            var menuItem1 = new ToolStripMenuItem();
            menuItem1.Text = "E&xit";
            menuItem1.Click += new EventHandler(ExitClick);

            var menuItem2 = new ToolStripMenuItem();
            menuItem2.Text = "Show";
            menuItem2.Click += new EventHandler(ShowClick);

            contextMenu1.Items.AddRange(
                        new ToolStripMenuItem[] { menuItem2, menuItem1 });

            _notifyIcon1 = new NotifyIcon(components);
            _notifyIcon1.Icon = new Icon("resources/SWTORParsingIcon.ico");
            _notifyIcon1.ContextMenuStrip = contextMenu1;
            _notifyIcon1.Text = System.Windows.Forms.Application.ProductName;
            _notifyIcon1.Visible = true;
            _notifyIcon1.DoubleClick += new EventHandler(notifyIcon1_DoubleClick);
        }

        private void ShowClick(object sender, EventArgs e)
        {
            Show();
            LoadingWindowFactory.MainWindowHidden = false;
            WindowState = WindowState.Normal;
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            LoadingWindowFactory.MainWindowHidden = false;
            WindowState = WindowState.Normal;
        }

        private void ExitClick(object sender, EventArgs e)
        {
            _actuallyClosing = true;
            Close();
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!_actuallyClosing && ShouldShowPopup.ReadShouldShowPopup("BackgroundDisabled"))
            {
                e.Cancel = true;
                if (ShouldShowPopup.ReadShouldShowPopup("BackgroundMonitoring"))
                {
                    LoadingWindowFactory.ShowBackgroundNotice();
                }
                LoadingWindowFactory.MainWindowHidden = true;
                MainWindowClosing.FireHidden();
                Hide();
            }
            else
            {
                _notifyIcon1.Visible = false;
                _notifyIcon1.Dispose();
                SwtorDetector.StopMonitoring();
                MainWindowClosing.FireClosing();
                Environment.Exit(0);
            }
        }
        private void Window_MouseLeave_1(object sender, System.Windows.Input.MouseEventArgs e)
        {
            OrbsWindowManager.SaveWindowSizeAndPosition(new OrbsWindowInfo { TopLeft = new System.Windows.Point { X = Left, Y = Top }, Width = ActualWidth, Height = ActualHeight });
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0)
                return;
            var tabInstance = e.AddedItems[0] as TabInstance;
            if (tabInstance == null)
                return;
            HeaderSelectionState.UpdateSelectedHeader(tabInstance.HeaderText);
        }
        private void RegisterHotKey(int hotkeyId, int key)
        {
            // Register the "Ctrl+Alt+L" hotkey
            if (RegisterHotKey(new WindowInteropHelper(this).Handle, hotkeyId, MOD_ALT|MOD_CONTROL,key))
            {
                // Hotkey registered successfully
                Logging.LogInfo("Registered hotkey with id: " + hotkeyId);
            }
            else
            {
                // Failed to register the hotkey
                Logging.LogError("Failed to register hotkey: " + key +" with id: "+ hotkeyId);
            }
        }

        private void UnregisterHotKey(int id)
        {
            // Unregister the hotkey
            UnregisterHotKey(new WindowInteropHelper(this).Handle, id);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            HwndSource source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Handle the WM_HOTKEY message
            if (msg == WM_HOTKEY)
            {
                if (wParam.ToInt32() == 1)
                {
                    HotkeyHandler.FireLockOverlay();
                    handled = true; // Mark the message as handled
                }
                if (wParam.ToInt32() == 2)
                {
                    HotkeyHandler.FireRefreshHots();
                    handled = true; // Mark the message as handled
                }
            }
            return IntPtr.Zero;
        }
    }
}
