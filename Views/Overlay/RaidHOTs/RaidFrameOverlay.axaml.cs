using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
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
        private DispatcherTimer _cursorTimer;
        public event Action<double, double> AreaClicked = delegate { };
        public event Action<bool> MouseInArea = delegate { };
        public RaidFrameOverlay(RaidFrameOverlayViewModel viewModel)
        {
            DataContext = viewModel;
            _viewModel = viewModel;
            InitializeComponent();

            Loaded += Hello;
            CombatLogStreamer.CombatUpdated += CheckForCombat;
            // fire at ~200 ms intervals, on the UI thread
            _cursorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _cursorTimer.Tick += OnCursorTimerTick;
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
            StartPolling();
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
        public void StartPolling() => _cursorTimer.Start();
        public void StopPolling()  => _cursorTimer.Stop();

        private void OnCursorTimerTick(object? sender, EventArgs e)
        {
            // only do work when we actually need to
            if (!_inCombat && _manuallyEditing)
            {
                var cursorPos = GetCursorPosition();
                var topLeft   = GetTopLeft();
                var width     = GetWidth();
                var height    = GetHeight();

                bool inside =  
                    cursorPos.X > topLeft.X &&
                    cursorPos.X < topLeft.X + width &&
                    cursorPos.Y > topLeft.Y &&
                    cursorPos.Y < topLeft.Y + height;

                if (inside) SubscribeToClicks();
                else        UnsubscribeFromClicks();
            }
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
                return new Point(0, 0);
                //return MouseHookHandler.GetCursorPosition();
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
            IntPtr cgEvent = CGEventCreate(IntPtr.Zero); // Create a new event
            if (cgEvent == IntPtr.Zero)
            {
                throw new InvalidOperationException("Failed to create CGEvent.");
            }

            CGPoint mousePosition = CGEventGetLocation(cgEvent);
            return new Point(mousePosition.X, mousePosition.Y);
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

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern IntPtr CGEventCreate(IntPtr source);

        [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
        private static extern CGPoint CGEventGetLocation(IntPtr cgEvent);
        private int GetHeight()
        {
            var parentWindow = VisualRoot as BaseOverlayWindow;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {

                var scalingFactor = desktop.MainWindow.RenderScaling;
                return (int)(parentWindow.Height - (87 * scalingFactor));
            }

            return 0;
        }
        private int GetWidth()
        {
            var parentWindow = VisualRoot as BaseOverlayWindow;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {

                var scalingFactor = desktop.MainWindow.RenderScaling;
                return (int)(parentWindow.Width - (100 * scalingFactor));
            }

            return 0;
        }
        private PixelPoint GetTopLeft()
        {        
            var parentWindow = VisualRoot as BaseOverlayWindow;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {

                var scalingFactor = desktop.MainWindow.RenderScaling;
                return new PixelPoint((int)(parentWindow.Position.X + (50 * scalingFactor)),
                    (int)(parentWindow.Position.Y + (87 * scalingFactor)));
            }

            return new PixelPoint();
        }
    }
}
