using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
    private bool _canClickThrough;

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
        //RemoveFromAppWindow();
        IdentifierText.Text = _viewModel._overlayName;
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * RenderScaling), Position.Y + (53 * RenderScaling)), new Point((Width - 100) * RenderScaling, (Height - 53 ) * RenderScaling));
        _myScreen = GetCurrentScreen(this);
    }

    private void SetSizeAndLocation(Point position, Point size)
    {
        CacheTempPositions(position, size);
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = new PixelPoint((int)position.X, (int)position.Y);
            Width = size.X;
            Height = size.Y;
            if ((_viewModel.MainContent is UserControl userControl))
            {
                userControl.Width = Width;
                userControl.Height = Height;
            }
        });
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

    private PixelPoint savedPosition;
    private Point savedSize;
    private Point savedObjectSize;
    private Screen? _myScreen;

    private void ToggleClickThrough(bool canClickThrough)
    {
        if(_canClickThrough == canClickThrough)
            return;
        _canClickThrough = canClickThrough;
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_viewModel.KeepBackgroundHidden)
            {
                if ((_viewModel.MainContent is UserControl userControl) )
                {
                    if (canClickThrough)
                    {
                        var scalingFactor = _myScreen.Scaling;
                        ContentCanvas.IsVisible = true;
                        ContentGrid.IsVisible = false;
                        ContentGrid.Children.Remove(ContentObject);
                        ContentCanvas.Children.Add(ContentObject);
                        ContentObject.Content = userControl;

                        savedPosition = Position;
                        savedSize = new Point(Width, Height);
                        savedObjectSize = new Point(userControl.Bounds.Width * scalingFactor, userControl.Bounds.Height * scalingFactor);
                        Position = new PixelPoint(0, 0);
                        Width = _myScreen.Bounds.Width / scalingFactor;
                        Height = _myScreen.Bounds.Height / scalingFactor;
                        ContentCanvas.Width = _myScreen.Bounds.Width /scalingFactor;
                        ContentCanvas.Height = _myScreen.Bounds.Height /scalingFactor;
                        userControl.Width = savedObjectSize.X / scalingFactor;
                        userControl.Height = savedObjectSize.Y / scalingFactor;
                        Canvas.SetLeft(ContentObject, savedPosition.X / scalingFactor + 5);
                        Canvas.SetTop(ContentObject, (savedPosition.Y / scalingFactor) +  2);
                    }
                    else
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