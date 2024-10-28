using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
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
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOOWNERZORDER = 0x0200;

    private static readonly IntPtr HWND_TOP = new IntPtr(0);

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
        ShowActivated = false;
        DataContext = viewModel;
        _viewModel = viewModel;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            RemoveShadowAndBorderMac();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            RemoveShadowAndBorderWindows();
        InitializeComponent();
        Loaded += InitOverlay;
        viewModel.OnLocking += ToggleClickThrough;
        viewModel.CloseRequested += Hide;
        viewModel.OnNewPositionAndSize += SetSizeAndLocation;
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
        ToggleClickThrough(!_viewModel.OverlaysMoveable);
        RemoveFromAppWindow();
        IdentifierText.Text = _viewModel._overlayName;
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * RenderScaling), Position.Y + (53 * RenderScaling)), new Point((Width - 100) * RenderScaling, (Height - 53 ) * RenderScaling));
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
    private void ToggleClickThrough(bool canClickThrough)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                MakeWindowClickThroughMac(canClickThrough);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                MakeWindowClickThroughWindows(canClickThrough);
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
// Final attempt to remove shadows and borders using SetWindowPos
    private void RemoveShadowAndBorderWindows()
    {
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null) return;

        var hWnd = platformHandle.Handle;
    
        // This style update removes borders and forces a frame change without shadow
        int windowStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
        SetWindowLong(hWnd, GWL_EXSTYLE, windowStyle | WS_EX_TOOLWINDOW & ~WS_EX_APPWINDOW);

        SetWindowPos(hWnd, HWND_TOP, 0, 0, 0, 0,
            SWP_FRAMECHANGED | SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private void RemoveShadowAndBorderMac()
    {
        var platformHandle = this.TryGetPlatformHandle();
        if (platformHandle == null) return;

        IntPtr nsWindowHandle = platformHandle.Handle;
        var setHasShadowSelector = sel_registerName("setHasShadow:");
        objc_msgSend(nsWindowHandle, setHasShadowSelector, false);
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
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * RenderScaling), Position.Y + (53* RenderScaling)), new Point((Width - 100) * RenderScaling, (Height - 53 ) * RenderScaling));
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