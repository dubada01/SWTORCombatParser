using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using SWTORCombatParser.Model.Overlays;
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

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

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

    public BaseOverlayWindow(BaseOverlayViewModel viewModel)
    {
        DataContext = viewModel;
        _viewModel = viewModel;

        InitializeComponent();
        Loaded += InitOverlay;
        viewModel.OnLocking += ToggleClickThrough;
        viewModel.CloseRequested += Close;
        viewModel.OnNewPositionAndSize += SetSizeAndLocation;
        if (viewModel.OverlaysMoveable)
            ToggleClickThrough(false);
        else
            ToggleClickThrough(true);
        Opened += SetWindowParams;
    }

    private void SetWindowParams(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = _tempLocation;
            Width = _tempSize.X;
            Height = _tempSize.Y;
        });
    }

    private void InitOverlay(object? sender, RoutedEventArgs e)
    {
        ToggleClickThrough(true);
        RemoveFromAppWindow();
        IdentifierText.Text = _viewModel._overlayName;
    }

    private void SetSizeAndLocation(Point position, Point size)
    {
        CacheTempPositions(position, size);
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = new PixelPoint((int)position.X, (int)position.Y);
            Width = size.X;
            Height = size.Y;
        });
    }

    private void CacheTempPositions(Point position, Point size)
    {
        _tempLocation = new PixelPoint((int)position.X, (int)position.Y);
        _tempSize = size;
    }
    public void ToggleClickThrough(bool canClickThrough)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                MakeWindowClickThroughMac(canClickThrough);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                MakeWindowClickThroughWindows(canClickThrough);
            BackgroundArea.Opacity = canClickThrough ? 0.1 : 0.75;
            CloseButton.IsVisible = !canClickThrough;
        });

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

    private void RemoveFromAppWindow()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RemoveFromWindowsTaskSwitcher();
        }
    }

    private void RemoveFromWindowsTaskSwitcher()
    {
        // Get the native window handle using Avalonia's GetPlatformHandle method
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null)
        {
            Console.WriteLine("Unable to retrieve platform handle.");
            return;
        }

        var hWnd = platformHandle.Handle;

        int extendedStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, (extendedStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW);
    }

    private void UpdateState()
    {
        _viewModel.UpdateWindowProperties(new Point(Position.X, Position.Y), new Point(Width, Height));
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