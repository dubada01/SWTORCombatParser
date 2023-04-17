using Gma.System.MouseKeyHook;
using Newtonsoft.Json;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SWTORCombatParser.Views.Overlay.RaidHOTs
{
    /// <summary>
    /// Interaction logic for RaidFrameOverlay.xaml
    /// </summary>
    public partial class RaidFrameOverlay : Window
    {
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int GWL_EXSTYLE = (-20);
        private const int WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080;
        private string _currentPlayerName = "no character";
        private IKeyboardMouseEvents _globalHook;
        private bool _inCombat;

        public event Action<double, double> AreaClicked = delegate { };
        public event Action<bool> MouseInArea = delegate { };
        public RaidFrameOverlay()
        {
            InitializeComponent();
            
            Loaded += Hello;
            CombatLogStreamer.CombatUpdated += CheckForCombat;
        }

        private void CheckForCombat(CombatStatusUpdate obj)
        {
            if(obj.Type == UpdateType.Start)
            {
                _inCombat= true;
                UnsubscribeFromClicks();
            }
            if(obj.Type == UpdateType.Stop)
            {
                _inCombat= false;
            }
        }

        private void GlobalMouseDown(object sender, MouseEventExtArgs e)
        {
            if (e.X < GetTopLeft().X || e.X > (GetTopLeft().X + GetWidth()) || e.Y < GetTopLeft().Y || e.Y > (GetTopLeft().Y + GetHeight()))
                return;
            var relativeX =e.X - GetTopLeft().X;
            var relativeY =e.Y - GetTopLeft().Y;
            var xFract = relativeX / (double)GetWidth();
            var yFract = relativeY / (double)GetHeight();
            AreaClicked(xFract, yFract);
        }

        private void RemoveFromAppWindow()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
        }
        
        public void SetPlayer(string playerName)
        {
            _currentPlayerName = playerName;
            var defaults = RaidFrameOverlayManager.GetDefaults(_currentPlayerName);
            Dispatcher.Invoke(() => {
                Width = defaults.WidtHHeight.X;
                Height = defaults.WidtHHeight.Y;
                Top = defaults.Position.Y;
                Left = defaults.Position.X;
            });
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(), Height, Width, GetTopLeft());
        }
        private void Hello(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(), Height, Width, GetTopLeft());
            viewModel.ToggleLocked += makeTransparent;
            viewModel.PlayerChanged += SetPlayer;
            
            RemoveFromAppWindow();
            PollForCursorPos();
        }

        private bool _isSubscribed;
        private bool _isLocked;

        private void SubscribeToClicks()
        {
            if(_isSubscribed)
                return;
            _isSubscribed = true;
            _globalHook = Hook.GlobalEvents();
            _globalHook.MouseDownExt += GlobalMouseDown;
            MouseInArea(true);
        }

        private void UnsubscribeFromClicks()
        {
            if (!_isSubscribed)
                return;
            _isSubscribed = false;
            _globalHook.MouseDownExt -= GlobalMouseDown;
            _globalHook.Dispose();
            MouseInArea(false);
        }
        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            try
            {
                DragMove();
                var viewModel = DataContext as RaidFrameOverlayViewModel;
                viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(), Height, Width, GetTopLeft());
                args.Handled = true;
            }
            catch(Exception e)
            {
                Logging.LogInfo("Failed to drag window: "+JsonConvert.SerializeObject(e));
            }

        }

        private void PollForCursorPos()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    POINT cursorPos = new POINT();
                    if (GetCursorPos(out cursorPos) && !_inCombat && !_isLocked)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var topLeft = GetTopLeft();
                            var width = GetWidth();
                            var height = GetHeight();
                            if (cursorPos.X > topLeft.X && cursorPos.X < topLeft.X + width && cursorPos.Y > topLeft.Y &&
                                cursorPos.Y < topLeft.Y + height)
                            {
                                SubscribeToClicks();
                            }
                            else
                            {
                                UnsubscribeFromClicks();
                            }
                        });
                    }
                    Thread.Sleep(200);
                }
            });
        }
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public static implicit operator System.Drawing.Point(POINT point)
            {
                return new System.Drawing.Point(point.X, point.Y);
            }
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
            return (int)(RaidGrid.ActualHeight * GetDPI().Item2);
        }
        private int GetWidth()
        {
            return (int)(RaidGrid.ActualWidth * GetDPI().Item1);
        }
        private System.Drawing.Point GetTopLeft()
        {
            var dpi = GetDPI();
            var realTop = (int)(Top * dpi.Item2);
            var realLeft = (int)((Left + 50) * dpi.Item1);
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
            _isLocked = shouldLock;
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
