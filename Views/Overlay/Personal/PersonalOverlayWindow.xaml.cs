using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.ViewModels.Overlays.Personal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.Overlay.Personal
{
    /// <summary>
    /// Interaction logic for PersonalOverlayWindow.xaml
    /// </summary>
    public partial class PersonalOverlayWindow : Window
    {
        private PersonalOverlayViewModel viewModel;
        private bool _closed;
        public PersonalOverlayWindow(PersonalOverlayViewModel vm)
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
            vm.CloseRequested += CloseOverlay;

            Loaded += OnLoaded;
        }

        private void ShowOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!_closed)
                    Show();
            });

        }

        private void HideOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Hide();
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
            makeTransparent(true);
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
            Dispatcher.Invoke(() => {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (shouldLock)
                {
                    BackgroundArea.Opacity = 0.1f;
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                }
                else
                {
                    BackgroundArea.Opacity = 0.45f;
                    //Remove the WS_EX_TRANSPARENT flag from the extended window style
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

                }
            });

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
            DefaultGlobalOverlays.SetDefault("Personal", new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height });
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
            DefaultGlobalOverlays.SetDefault("Personal", new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height });
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
