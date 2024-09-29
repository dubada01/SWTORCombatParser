using Gma.System.MouseKeyHook;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace SWTORCombatParser.Views.Overlay.RaidHOTs
{
    /// <summary>
    /// Interaction logic for RaidFrameOverlay.xaml
    /// </summary>
    public partial class RaidFrameOverlay : BaseOverlayWindow
    {
        private IKeyboardMouseEvents _globalHook;
        private bool _inCombat;
        private bool _isSubscribed;
        private bool _isLocked = true;
        private readonly RaidFrameOverlayViewModel _viewModel;
        public event Action<double, double> AreaClicked = delegate { };
        public event Action<bool> MouseInArea = delegate { };
        public RaidFrameOverlay(RaidFrameOverlayViewModel viewModel):base(viewModel)
        {
            DataContext = viewModel;
            _viewModel = viewModel;
            InitializeComponent();

            Loaded += Hello;
            CombatLogStreamer.CombatUpdated += CheckForCombat;
        }

        private void CheckForCombat(CombatStatusUpdate obj)
        {
            if (obj.Type == UpdateType.Start)
            {
                _inCombat = true;
                UnsubscribeFromClicks();
            }
            if (obj.Type == UpdateType.Stop)
            {
                _inCombat = false;
            }
        }

        private void GlobalMouseDown(object sender, MouseEventExtArgs e)
        {
            if (e.X < GetTopLeft().X || e.X > (GetTopLeft().X + GetWidth()) || e.Y < GetTopLeft().Y || e.Y > (GetTopLeft().Y + GetHeight()))
                return;
            var relativeX = e.X - GetTopLeft().X;
            var relativeY = e.Y - GetTopLeft().Y;
            var xFract = relativeX / (double)GetWidth();
            var yFract = relativeY / (double)GetHeight();
            AreaClicked(xFract, yFract);
        }
        
        public new void SetPlayer(string playerName)
        {
            _currentPlayerName = playerName;
            var defaults = RaidFrameOverlayManager.GetDefaults(_currentPlayerName);
            Dispatcher.UIThread.Invoke(() =>
            {
                Width = defaults.WidtHHeight.X;
                Height = defaults.WidtHHeight.Y;
                Position = new PixelPoint((int)defaults.Position.X, (int)defaults.Position.Y);
            });
            var viewModel = DataContext as RaidFrameOverlayViewModel;
            viewModel.UpdatePositionAndSize(GetHeight(), GetWidth(), Height, Width, GetTopLeft());
        }
        private void Hello(object? sender, RoutedEventArgs routedEventArgs)
        {
            PollForCursorPos();
        }
        
        private void SubscribeToClicks()
        {
            if (_isSubscribed)
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
        private void PollForCursorPos()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (!_inCombat && !_isLocked)
                    {
                        var cursorPos = GetCursorPosition();
                        Dispatcher.UIThread.Invoke(() =>
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
        // Method to get the cursor position cross-platform
        public static Point GetCursorPosition()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCursorPositionWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCursorPositionMac();
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform");
            }
        }

        // Windows-specific function
        private static Point GetCursorPositionWindows()
        {
            GetCursorPos(out POINT point);
            return new Point(point.X, point.Y);
        }

        // MacOS-specific function
        private static Point GetCursorPositionMac()
        {
            CGPoint point = CGEventSourceGetCursorPosition();
            return new Point(point.X, point.Y);
        }
        // Structs for Windows and MacOS
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CGPoint
        {
            public double X;
            public double Y;
        }

        // P/Invoke for Windows
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        // P/Invoke for MacOS
        [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
        private static extern CGPoint CGEventSourceGetCursorPosition();
        private int GetHeight()
        {
            return (int)((RaidGrid.Height));
        }
        private int GetWidth()
        {
            return (int)((RaidGrid.Width));
        }
        private System.Drawing.Point GetTopLeft()
        {
            var realTop = (int)((Position.Y + 50));
            var realLeft = (int)((Position.X + 50));
            return new System.Drawing.Point(realLeft, realTop);
        }
    }
}
