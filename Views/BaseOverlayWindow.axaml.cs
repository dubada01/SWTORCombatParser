using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Overlays;

namespace SWTORCombatParser.Views;


public enum OverlaySettingsType
{
    Global,
    Character
}

public partial class BaseOverlayWindow : Window
{
    private bool _isDragging;
    private Point _startPoint;
    
    // Windows-specific constants for P/Invoke
    const int GWL_EXSTYLE = -20;
    const int WS_EX_LAYERED = 0x00080000;
    const int WS_EX_TRANSPARENT = 0x00000020;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int WS_EX_APPWINDOW = 0x00040000;
    

    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    
    
    
    
    // P/Invoke for Ubuntu X11 library
    [DllImport("libX11.so")]
    private static extern IntPtr XOpenDisplay(IntPtr display);

    [DllImport("libX11.so")]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so")]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport("libX11.so")]
    private static extern void XChangeProperty(IntPtr display, IntPtr w, IntPtr property, int type, int format,
        int mode, ref IntPtr data, int nelements);

    private const int PropModeReplace = 0;
    
   

    // P/Invoke to interact with Objective-C runtime and Cocoa APIs
    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
    public static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_getClass")]
    public static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend(IntPtr receiver, IntPtr selector, bool arg1);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    public static extern void objc_msgSend_float(IntPtr receiver, IntPtr selector, float value);

    private PixelPoint _tempLocation;
    private Point _tempSize;
    private readonly BaseOverlayViewModel _viewModel;
    public bool _canClickThrough = false;

    public BaseOverlayWindow(BaseOverlayViewModel viewModel)
    {
        ShowActivated = false;
        DataContext = viewModel;
        _viewModel = viewModel;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            RemoveShadowAndBorderMac();

        
        InitializeComponent();
        Loaded += InitOverlay;
        viewModel.OnLocking += ToggleClickThrough;
        viewModel.CloseRequested += Hide;
        viewModel.OnNewPositionAndSize += SetSizeAndLocation;
        Opened += SetWindowParams;
    }

    private void SetWindowParams(object? sender, EventArgs e)
    {
        string? obsTitle = _viewModel is OverlayInstanceViewModel overlay
            ? overlay.Type == OverlayType.None
                ? null
                : $"{overlay.Type}"
            : _viewModel.ToString()!.Split('.').Last().Replace("ViewModel", "");
        if (obsTitle is not null) base.Title = obsTitle;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            RemoveFromAltTab();
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = _tempLocation;
            Width = _tempSize.X;
            Height = _tempSize.Y;
        });
    }

    private void InitOverlay(object? sender, RoutedEventArgs e)
    {
        ToggleClickThrough(!_viewModel.OverlaysMoveable);
        IdentifierText.Text = _viewModel._overlayName;
        #if WINDOWS
        var renderScaling = RenderScaling;
        #endif
        #if MACOS
        var renderScaling = 1;
        #endif
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * renderScaling), Position.Y + (78 * renderScaling)), new Point((Width - 100) * renderScaling, (Height - 78 ) * renderScaling));
    }

    private void SetSizeAndLocation(Point position, Point size)
    {
        CacheTempPositions(position, size);
        var shouldSetClickthrough = _canClickThrough;
        if (_canClickThrough)
        {
            ToggleClickThrough(false);
        }
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = new PixelPoint((int)position.X, (int)position.Y);
            Width = size.X;
            Height = size.Y;
        });

        if (shouldSetClickthrough)
        {
            _canClickThrough = false;
            ToggleClickThrough(true);
        }
    }

    private void CacheTempPositions(Point position, Point size)
    {
        _tempLocation = new PixelPoint((int)position.X, (int)position.Y);
        _tempSize = size;
    }
    


    private void ToggleClickThrough(bool canClickThrough)
    {
        if(_canClickThrough == canClickThrough)
            return;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ToggleClickThroughCrossPlatform(canClickThrough);
            BackgroundArea.Opacity = canClickThrough ? _viewModel.BackgroundLockedOpacity : _viewModel.BackgroundUnLockedOpacity;
            OverlayIdText.IsVisible = !canClickThrough;
            CloseButton.IsVisible = !canClickThrough;
        });
        _canClickThrough = canClickThrough;
    }

    private void RemoveFromAltTab()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var visualRoot = this.GetVisualRoot() as TopLevel;
            if (visualRoot != null && visualRoot.TryGetPlatformHandle() is { } platformHandle)
            {
                var hwnd = platformHandle.Handle;
                SetWindowLong(hwnd, GWL_EXSTYLE,
                    GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW);
            }
        });
    }
    
    public void ToggleClickThroughCrossPlatform(bool canClickThrough)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            MakeWindowClickThroughMac(canClickThrough);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            MakeWindowClickThroughWindows(canClickThrough);
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            MakeWindowClickThroughUbuntu(canClickThrough);
    }

    // Platform-specific method for Windows
    private void MakeWindowClickThroughWindows(bool isClickThrough)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            // Get the native window handle using Avalonia's GetPlatformHandle method
            var platformHandle = this.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                return;
            }

            var hWnd = platformHandle.Handle;

            // Get the current extended style
            int extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);

            if (isClickThrough)
            {
                // Make the window click-through
                SetWindowLong(hWnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TRANSPARENT);
            }
            else
            {
                // Make the window clickable again by removing the WS_EX_TRANSPARENT flag
                SetWindowLong(hWnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
            }
        });

    }

    private void MakeWindowClickThroughMac(bool isClickThrough)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            // Get the native NSWindow handle using Avalonia's GetPlatformHandle method
            var platformHandle = this.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                return;
            }

            IntPtr nsWindowHandle = platformHandle.Handle;

            // Get the selector for 'setIgnoresMouseEvents:'
            var setIgnoresMouseEventsSelector = sel_registerName("setIgnoresMouseEvents:");

            // Call the 'setIgnoresMouseEvents' method with the boolean argument
            objc_msgSend(nsWindowHandle, setIgnoresMouseEventsSelector, isClickThrough);
        });

    }
    // Platform-specific method for Ubuntu
    private void MakeWindowClickThroughUbuntu(bool isClickThrough)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            // Get the native window handle using Avalonia's GetPlatformHandle method
            var platformHandle = this.TryGetPlatformHandle();
            if (platformHandle == null)
            {
                return;
            }

            IntPtr x11WindowHandle = platformHandle.Handle;

            IntPtr display = XOpenDisplay(IntPtr.Zero);
            if (display == IntPtr.Zero)
            {
                throw new Exception("Unable to open X11 display.");
            }

            // Set the window to be click-through
            var prop = XInternAtom(display, "_NET_WM_WINDOW_TYPE", false);
            var type = isClickThrough
                ? XInternAtom(display, "_NET_WM_WINDOW_TYPE_DOCK", false)
                : XInternAtom(display, "_NET_WM_WINDOW_TYPE_NORMAL", false);

            XChangeProperty(display, x11WindowHandle, prop, 4, 32, PropModeReplace, ref type, 1);
            XCloseDisplay(display);
        });
    }

    private void RemoveShadowAndBorderMac()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var platformHandle = this.TryGetPlatformHandle();
            if (platformHandle == null) return;

            IntPtr nsWindowHandle = platformHandle.Handle;
            var setHasShadowSelector = sel_registerName("setHasShadow:");
            objc_msgSend(nsWindowHandle, setHasShadowSelector, false);
        });
    }


    private void UpdateState()
    {
        _viewModel.UpdateWindowProperties(new Point(Position.X, Position.Y), new Point(Width, Height));
#if WINDOWS
        var renderScaling = RenderScaling;
#endif
#if MACOS
        var renderScaling = 1;
#endif
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * renderScaling), Position.Y + (78* renderScaling)), new Point((Width - 100) * renderScaling, (Height - 78 ) * renderScaling));
        CacheTempPositions(new Point(Position.X, Position.Y), new Point(Width, Height));
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        _startPoint = e.GetPosition(this);
    }

    private void StopDragWindow(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        UpdateState();
    }

    private void Border_MouseEnter(object? sender, PointerEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Cursor = new Cursor(StandardCursorType.DragMove);
        });
    }

    private void Border_MouseMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            // Get the current scaling factor to adjust the movement correctly
            var scalingFactor = this.VisualRoot.RenderScaling;

            var currentPosition = e.GetPosition(this);
            var delta = (currentPosition - _startPoint) / scalingFactor; // Adjust for DPI scaling

            // Move the window (or element) by the delta
            var currentPositionInScreen = this.Position;
            this.Position = new PixelPoint(
                currentPositionInScreen.X + (int)delta.X,
                currentPositionInScreen.Y + (int)delta.Y
            );
        }
    }

    private void Grid_MouseLeave(object? sender, PointerEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Cursor = new Cursor(StandardCursorType.Arrow);
            UpdateState();
        });
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(_viewModel.CloseButtonClicked);
    }

    private void Thumb_MouseEnter(object? sender, PointerEventArgs e)
    {
        Dispatcher.UIThread.Invoke(() => { Cursor = new Cursor(StandardCursorType.BottomRightCorner); });
    }
    private Point startDrag;
    private double initialWidth;
    private double initialHeight;

    private void Drag_Started(object? sender, PointerPressedEventArgs e)
    {
        startDrag = e.GetPosition(this);
        initialWidth = Width;
        initialHeight = Height;
        _isDragging = true;
    }

    private void Thumb_DragDelta(object? sender, PointerEventArgs e)
    {
        if(!_isDragging)
            return;
        Dispatcher.UIThread.Invoke(() =>
        {
            var currentPosition = e.GetPosition(this);
            var delta = currentPosition - startDrag;

            var newWidth = initialWidth + delta.X;
            var newHeight = initialHeight + delta.Y;

            // Ensure we don't set negative dimensions.
            if (newWidth > 0)
                SetValue(WidthProperty, newWidth);
            if (newHeight > 0)
                SetValue(HeightProperty, newHeight);

            UpdateState();
        });
    }

    private void Drag_Stopped(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
    }
}