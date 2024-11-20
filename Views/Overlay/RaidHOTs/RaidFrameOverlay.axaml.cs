using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ScottPlot;
using SWTORCombatParser.Utilities.MouseHandler;
using RoutedEventArgs = Avalonia.Interactivity.RoutedEventArgs;

namespace SWTORCombatParser.Views.Overlay.RaidHOTs
{
    /// <summary>
    /// Interaction logic for RaidFrameOverlay.xaml
    /// </summary>
    public partial class RaidFrameOverlay : UserControl
    {
        private MouseHookHandler _mouseHookHandler;
        private bool _inCombat;
        private bool _isSubscribed;
        public bool _manuallyEditing = false;
        private readonly RaidFrameOverlayViewModel _viewModel;
        public event Action<double, double> AreaClicked = delegate { };
        public event Action<bool> MouseInArea = delegate { };
        public RaidFrameOverlay(RaidFrameOverlayViewModel viewModel)
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

        private void GlobalMouseDown(Point e)
        {
            var cursorPos = GetCursorPosition();
            Debug.WriteLine("Global Mouse Down: " + cursorPos);
            if (cursorPos.X < GetTopLeft().X || cursorPos.X > (GetTopLeft().X + GetWidth()) || cursorPos.Y < GetTopLeft().Y || cursorPos.Y > (GetTopLeft().Y + GetHeight()))
                return;
            var relativeX = cursorPos.X - GetTopLeft().X;
            var relativeY = cursorPos.Y - GetTopLeft().Y;
            var xFract = relativeX / (double)GetWidth();
            var yFract = relativeY / (double)GetHeight();
            AreaClicked(xFract, yFract);
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
            _mouseHookHandler = new MouseHookHandler();
            _mouseHookHandler.SubscribeToClicks();
            _mouseHookHandler.MouseClicked += GlobalMouseDown;
            MouseInArea(true);
        }

        private void UnsubscribeFromClicks()
        
        {
            if (!_isSubscribed)
                return;
            _isSubscribed = false;
            _mouseHookHandler.UnsubscribeFromClicks();
            _mouseHookHandler.MouseClicked -= GlobalMouseDown;
            _mouseHookHandler = null;
            MouseInArea(false);
        }
        private void PollForCursorPos()
        {
            Task.Run(() =>
            {
                while (true)
                {
                    if (!_inCombat && _manuallyEditing)
                    {
                        var cursorPos = GetCursorPosition();
                        Dispatcher.UIThread.Invoke(() =>
                        {
                            var topLeft = GetTopLeft();
                            var width = GetWidth();
                            var height = GetHeight();
                            //Debug.WriteLine("Cursor Pos: " + cursorPos + " TopLeft: " + topLeft + " Width: " + width + " Height: " + height);
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
        public Point GetCursorPosition()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCursorPositionWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return GetCursorPositionMac();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return MouseHookHandler.GetCursorPosition();
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
            var parentWindow = VisualRoot as BaseOverlayWindow;
            var scalingFactor = parentWindow.Screens.ScreenFromVisual(parentWindow).Scaling;
            return (int)(parentWindow.savedObjectSize.Y - (87 * scalingFactor));
        }
        private int GetWidth()
        {
            var parentWindow = VisualRoot as BaseOverlayWindow;
            var scalingFactor = parentWindow.Screens.ScreenFromVisual(parentWindow).Scaling;
            return (int)(parentWindow.savedObjectSize.X - (100 * scalingFactor));
        }
        private PixelPoint GetTopLeft()
        {        
            var parentWindow = VisualRoot as BaseOverlayWindow;
            var scalingFactor = parentWindow.Screens.ScreenFromVisual(parentWindow).Scaling;
            return new PixelPoint((int)(parentWindow.savedPosition.X + (50 * scalingFactor)), (int)(parentWindow.savedPosition.Y + (87 * scalingFactor)));
        }
    }
}
