using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace SWTORCombatParser.Views.Overlay
{
    /// <summary>
    /// Interaction logic for InfoOverlay.xaml
    /// </summary>
    public partial class InfoOverlay : Window
    {
        private OverlayInstanceViewModel viewModel;
        private string _currentPlayerName;
        private bool _closed;
        private bool _hidden;
        public InfoOverlay(OverlayInstanceViewModel vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Close,
                new ExecutedRoutedEventHandler(delegate (object sender, ExecutedRoutedEventArgs args) { this.Close(); })));
            MainWindowClosing.Closing += CloseOverlay;
            vm.OnLocking += makeTransparent;
            vm.OnHiding += HideOverlay;
            vm.OnShowing += ShowOverlay;
            vm.OnCharacterDetected += SetPlayer;
            vm.CloseRequested += CloseOverlay;
            HotkeyHandler.OnHideOverlaysHotkey += ToggleHide;
            Loaded += OnLoaded;
        }
        private void ToggleHide()
        {
            if(_hidden)
            {
                ShowOverlay();
            }
            else
            {
                HideOverlay();
            }
        }
        private void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!_closed)
                {
                    Show();
                    _hidden = false;
                }
            });

        }

        private void HideOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Hide();
                _hidden = true;
            });
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            RemoveFromAppWindow();
        }

        private void RemoveFromAppWindow()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = (-20);
        private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        public void makeTransparent(bool shouldLock)
        {
            Dispatcher.Invoke(() =>
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (shouldLock)
                {
                    BackgroundArea.Opacity = 0.1f;
                    ScrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                }
                else
                {
                    BackgroundArea.Opacity = 0.45f;
                    ScrollView.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                    //Remove the WS_EX_TRANSPARENT flag from the extended window style
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

                }
            });

        }
        public void SetPlayer(string playerName)
        {
            _currentPlayerName = playerName;
        }
        private void CloseOverlay()
        {
            Dispatcher.Invoke(() =>
            {
                _closed = true;

                Close();
            });

        }

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
        }
        public void UpdateDefaults(object sender, MouseButtonEventArgs args)
        {
            DefaultCharacterOverlays.SetCharacterDefaults(viewModel.Type.ToString(), new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height }, _currentPlayerName);
        }

        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var yadjust = Height + e.VerticalChange;
            var xadjust = Width + e.HorizontalChange;
            if (xadjust > 0)
                SetValue(WidthProperty, xadjust);
            if (yadjust > 0)
                SetValue(HeightProperty, yadjust);
        }
        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            DefaultCharacterOverlays.SetCharacterDefaults(viewModel.Type.ToString(), new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height }, _currentPlayerName);
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            if (viewModel.OverlaysMoveable)
            {
                Mouse.OverrideCursor = Cursors.SizeNWSE;
            }
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            if (viewModel.OverlaysMoveable)
            {
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.Arrow;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            viewModel.OverlayClosing();
            CloseOverlay();
        }
    }
}
