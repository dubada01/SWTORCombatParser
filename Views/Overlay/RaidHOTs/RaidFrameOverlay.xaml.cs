using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
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

namespace SWTORCombatParser.Views.Overlay.RaidHOTs
{
    /// <summary>
    /// Interaction logic for RaidFrameOverlay.xaml
    /// </summary>
    public partial class RaidFrameOverlay : Window
    {
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = (-20);
        private string _currentPlayerName = "no character";
        public RaidFrameOverlay()
        {
            InitializeComponent();
            Loaded += Hello;
        }
        public void SetPlayer(string playerName)
        {
            _currentPlayerName = playerName;
            var defaults = RaidFrameOverlayManager.GetDefaults(_currentPlayerName);
            Width = defaults.WidtHHeight.X;
            Height = defaults.WidtHHeight.Y;
            Top = defaults.Position.Y;
            Left = defaults.Position.X;
        }
        private void Hello(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(), Height, Width, GetTopLeft());
            viewModel.ToggleLocked += makeTransparent;
            viewModel.PlayerChanged += SetPlayer;
        }

        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(),Height,Width, GetTopLeft());
        }
        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {

            Mouse.OverrideCursor = Cursors.Hand;

        }
        public void UpdateDefaults(object sender, MouseButtonEventArgs args)
        {
            RaidFrameOverlayManager.SetDefaults(new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height }, _currentPlayerName);
        }
        private void Grid_MouseLeave(object sender, MouseEventArgs e)
        {
            RaidFrameOverlayManager.SetDefaults(new Point() { X = Left, Y = Top }, new Point() { X = Width, Y = Height }, _currentPlayerName);
            Mouse.OverrideCursor = Cursors.Arrow;
        }
        private void Thumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var yadjust = Height + e.VerticalChange;
            var xadjust = Width + e.HorizontalChange;
            if (xadjust > 0)
                SetValue(WidthProperty, xadjust);
            if (yadjust > 0)
                SetValue(HeightProperty, yadjust);
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(),GetWidth(), Height, Width, GetTopLeft());
        }
        private void Thumb_MouseEnter(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = Cursors.SizeNWSE;
        }
        private int GetHeight()
        {
            return (int)((ActualHeight - 50) * GetDPI().Item2);
        }
        private int GetWidth()
        {
            return (int)(ActualWidth * GetDPI().Item1);
        }
        private System.Drawing.Point GetTopLeft()
        {
            var dpi = GetDPI();
            var realTop = (int)((Top + 50) * dpi.Item2);
            var realLeft = (int)(Left * dpi.Item1);
            return new System.Drawing.Point(realLeft, realTop);
        }
        private (double,double) GetDPI()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            var dpiX = source.CompositionTarget.TransformToDevice.M11;

            var dpiY = source.CompositionTarget.TransformToDevice.M22;
            return (dpiX, dpiY);
        }

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
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
                }
                else
                {
                    int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);

                }
            });

        }
    }
}
