using System;
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
using SWTORCombatParser.ViewModels;

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
        _myScreen = GetCurrentScreen(this) ?? Screens.Primary;
    }

    private void SetWindowParams(object? sender, EventArgs e)
    {
        if(_canClickThrough && _viewModel.KeepBackgroundHidden)
            return;
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
        _myScreen = GetCurrentScreen(this) ?? Screens.Primary;
    }

    private void SetSizeAndLocation(Point position, Point size)
    {
        if (_canClickThrough && _viewModel.KeepBackgroundHidden)
        {
            _tempLocation = new PixelPoint((int)position.X, (int)position.Y);
            _tempSize = size;
            savedPosition = new PixelPoint((int)position.X, (int)position.Y);
            savedSize = size;
            savedObjectSize = size;
            _canClickThrough = false;
            ToggleClickThrough(true);
        }
        else
        {
            CacheTempPositions(position, size);
            Dispatcher.UIThread.Invoke(() =>
            {
                Position = new PixelPoint((int)position.X, (int)position.Y);
                Width = size.X;
                Height = size.Y;
            });
        }
    }

    private void CacheTempPositions(Point position, Point size)
    {
        _tempLocation = new PixelPoint((int)position.X, (int)position.Y);
        _tempSize = size;
    }
    
    public Screen? GetCurrentScreen(Window window)
    {
        // Get the window bounds in screen coordinates
        var windowBounds = window.Bounds;

        // Iterate over all screens and find the one with the most overlap
        Screen? targetScreen = null;
        double maxOverlapArea = 0;

        foreach (var screen in window.Screens.All)
        {
            // Convert PixelRect to Rect
            var screenBounds = new Rect(screen.Bounds.Position.ToPoint(1), screen.Bounds.Size.ToSize(1));


            // Calculate the intersection area between the window and the screen
            var intersection = windowBounds.Intersect(screenBounds);
            var overlapArea = intersection.Width * intersection.Height;

            // Check if this screen has the most overlap with the window
            if (overlapArea > maxOverlapArea)
            {
                maxOverlapArea = overlapArea;
                targetScreen = screen;
            }
        }

        return targetScreen;
    }

    public PixelPoint savedPosition;
    private Point savedSize;
    public Point savedObjectSize;
    private Screen? _myScreen;

    private void ToggleClickThrough(bool canClickThrough)
    {
        if(_canClickThrough == canClickThrough)
            return;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_viewModel.KeepBackgroundHidden)
            {
                if ((_viewModel.MainContent is UserControl userControl) )
                {
                    if (canClickThrough && !ContentCanvas.Children.Any())
                    {
                        ExpandWindowForClickthrough();
                    }
                    if(!canClickThrough && !ContentGrid.Children.Any())
                    {
                        ContentCanvas.IsVisible = false;
                        ContentGrid.IsVisible = true;
                        ContentCanvas.Children.Remove(ContentObject);
                        ContentGrid.Children.Add(ContentObject);
                        ContentObject.Content = userControl;

                        Position = savedPosition;
                        Width = savedSize.X;
                        Height = savedSize.Y;
                        
                        
                        Task.Run(() =>
                        {
                            Thread.Sleep(100);
                            Dispatcher.UIThread.Invoke(() =>
                            {
                                userControl.Width = double.NaN;
                                userControl.Height = double.NaN;
                            });
                        });


                    }
                }
            }
            ToggleClickThroughCrossPlatform(canClickThrough);

            if(!_viewModel.KeepBackgroundHidden)
                BackgroundArea.Opacity = canClickThrough ? 0.066 : 0.75;
            if (_viewModel.KeepBackgroundHidden)
            {
                BackgroundArea.Opacity = 0;
                OverlayIdText.IsVisible = false;
            }
            else
                OverlayIdText.IsVisible = !canClickThrough;
            CloseButton.IsVisible = !canClickThrough;
        });
        _canClickThrough = canClickThrough;
    }

    public void ExpandWindowForClickthrough()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if ((_viewModel.MainContent is not UserControl userControl) || ContentCanvas.Children.Any()) return;
            var scalingFactor = _myScreen.Scaling;
            ContentCanvas.IsVisible = true;
            ContentGrid.IsVisible = false;
            ContentGrid.Children.Remove(ContentObject);
            ContentCanvas.Children.Add(ContentObject);
            ContentObject.Content = userControl;

            savedPosition = Position;
            savedSize = new Point(Width, Height);
            savedObjectSize =
                new Point(userControl.Bounds.Width * scalingFactor, userControl.Bounds.Height * scalingFactor);
            Position = new PixelPoint(0, 0);
            Width = _myScreen.Bounds.Width / scalingFactor;
            Height = _myScreen.Bounds.Height / scalingFactor;
            ContentCanvas.Width = _myScreen.Bounds.Width / scalingFactor;
            ContentCanvas.Height = _myScreen.Bounds.Height / scalingFactor;
            userControl.Width = savedObjectSize.X / scalingFactor;
            userControl.Height = savedObjectSize.Y / scalingFactor;
            Canvas.SetLeft(ContentObject, savedPosition.X / scalingFactor + 4 * scalingFactor);
            Canvas.SetTop(ContentObject, (savedPosition.Y / scalingFactor) + 10 * scalingFactor);
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
        // Get the native window handle using Avalonia's GetPlatformHandle method
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null)
        {
            Console.WriteLine("Unable to retrieve platform handle.");
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
    }

    private void MakeWindowClickThroughMac(bool isClickThrough)
    {
        // Get the native NSWindow handle using Avalonia's GetPlatformHandle method
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null)
        {
            Console.WriteLine("Unable to retrieve platform handle.");
            return;
        }

        IntPtr nsWindowHandle = platformHandle.Handle;

        // Get the selector for 'setIgnoresMouseEvents:'
        var setIgnoresMouseEventsSelector = sel_registerName("setIgnoresMouseEvents:");

        // Call the 'setIgnoresMouseEvents' method with the boolean argument
        objc_msgSend(nsWindowHandle, setIgnoresMouseEventsSelector, isClickThrough);
    }
    // Platform-specific method for Ubuntu
    private void MakeWindowClickThroughUbuntu(bool isClickThrough)
    {
        // Get the native window handle using Avalonia's GetPlatformHandle method
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null)
        {
            Console.WriteLine("Unable to retrieve platform handle.");
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
    }

    private void RemoveShadowAndBorderMac()
    {
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null) return;

        IntPtr nsWindowHandle = platformHandle.Handle;
        var setHasShadowSelector = sel_registerName("setHasShadow:");
        objc_msgSend(nsWindowHandle, setHasShadowSelector, false);
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
        Cursor = new Cursor(StandardCursorType.DragMove);
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
        Cursor = new Cursor(StandardCursorType.Arrow);
        UpdateState();
    }

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(_viewModel.CloseButtonClicked);
    }

    private void Thumb_MouseEnter(object? sender, PointerEventArgs e)
    {
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    private void Thumb_DragDelta(object? sender, VectorEventArgs e)
    {
        var yadjust = Height + e.Vector.Y;
        var xadjust = Width + e.Vector.X;
        if (xadjust > 0)
            SetValue(WidthProperty, xadjust);
        if (yadjust > 0)
            SetValue(HeightProperty, yadjust);
        UpdateState();
    }
}