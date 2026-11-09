using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Native;
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


    [DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private PixelPoint _tempLocation;
    private Point _tempSize;
    private readonly BaseOverlayViewModel _viewModel;
    public bool _canClickThrough = false;
    private bool _appearanceSubscribed;
    private OverlayAppearance _appearance;
    private bool _showInObs;
    private bool _closed;
    private bool _nativeApplyQueued;
    private readonly DispatcherTimer _inputRepairTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private int _inputRepairAttempts;

    public BaseOverlayWindow(BaseOverlayViewModel viewModel)
    {
        ShowActivated = false;
        DataContext = viewModel;
        _viewModel = viewModel;

        InitializeComponent();
        if (OperatingSystem.IsWindows())
            _showInObs = Settings.ReadSettingOfType<bool>(Settings.ShowOverlaysInObsSetting);
        _appearance = OverlayAppearanceSettings.ReadAppearance();
        OverlayAppearanceSettings.AppearanceChanged += AppearanceSettingsChanged;
        _appearanceSubscribed = true;
        ApplyAppearance();
        Settings.SettingsUpdated += OverlaySettingsChanged;
        ApplyPresentation();
        Closed += (_, _) =>
        {
            OverlayAppearanceSettings.AppearanceChanged -= AppearanceSettingsChanged;
            Settings.SettingsUpdated -= OverlaySettingsChanged;
            _appearanceSubscribed = false;
            _closed = true;
            _inputRepairTimer.Stop();
            viewModel.OnLocking -= ToggleClickThrough;
            viewModel.CloseRequested -= Hide;
            viewModel.OnNewPositionAndSize -= SetSizeAndLocation;
            OverlayPlatformStatus.Remove(Title);
        };
        Loaded += InitOverlay;
        viewModel.OnLocking += ToggleClickThrough;
        viewModel.CloseRequested += Hide;
        viewModel.OnNewPositionAndSize += SetSizeAndLocation;
        Opened += SetWindowParams;
        _inputRepairTimer.Tick += (_, _) =>
        {
            if (!IsVisible || --_inputRepairAttempts <= 0) _inputRepairTimer.Stop();
            if (IsVisible) ApplyNativeInput();
        };
        SizeChanged += (_, _) => QueueNativeInput();
        PositionChanged += (_, _) => QueueNativeInput();
        PropertyChanged += (_, e) =>
        {
            if (e.Property == ActualTransparencyLevelProperty) QueueNativeInput();
        };

    }

    private void SetOverlayTitle() => Title = $"Orbs · {_viewModel.OverlayDisplayName}";

    private void SetWindowParams(object? sender, EventArgs e)
    {
        SetOverlayTitle();
        if (OperatingSystem.IsWindows())
            ApplyObsCaptureStyle();
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = _tempLocation;
            Width = _tempSize.X;
            Height = _tempSize.Y;
        });
        // Avalonia reapplies native window styles when a hidden window is shown.
        // Restore input transparency after that work, even when our cached lock
        // state has not changed since the previous showing.
        ToggleClickThrough(!_viewModel.OverlaysMoveable);
        QueueNativeInput();
    }

    private void InitOverlay(object? sender, RoutedEventArgs e)
    {
        ToggleClickThrough(!_viewModel.OverlaysMoveable);
        IdentifierText.Text = _viewModel.OverlayDisplayName;
        #if WINDOWS
        var renderScaling = RenderScaling;
        #endif
        #if MACOS
        var renderScaling = 1;
        #endif
        #if LINUX
        var renderScaling = 1;
        #endif
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * renderScaling), Position.Y + (78 * renderScaling)), new Point((Width - 100) * renderScaling, (Height - 78 ) * renderScaling));
    }

    private void SetSizeAndLocation(Point position, Point size)
    {
        CacheTempPositions(position, size);
        Dispatcher.UIThread.Invoke(() =>
        {
            Position = new PixelPoint((int)position.X, (int)position.Y);
            Width = size.X;
            Height = size.Y;
            QueueNativeInput();
        });
    }

    private void CacheTempPositions(Point position, Point size)
    {
        _tempLocation = new PixelPoint((int)position.X, (int)position.Y);
        _tempSize = size;
    }

    private void ToggleClickThrough(bool canClickThrough)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (_closed) return;
            _canClickThrough = canClickThrough;
            ApplyAppearance(canClickThrough);
            OverlayIdText.IsVisible = !canClickThrough;
            CloseButton.IsVisible = !canClickThrough;
            ApplyNativeInput();
            QueueNativeInput();
        });
    }

    private void QueueNativeInput()
    {
        if (_closed || !IsVisible || _nativeApplyQueued) return;
        _nativeApplyQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _nativeApplyQueued = false;
            if (_closed || !IsVisible) return;
            ApplyNativeInput();
            // Mapping and GPU child-window creation may finish after Opened/Loaded.
            if (!OperatingSystem.IsWindows() && IsVisible)
            {
                _inputRepairAttempts = 4;
                _inputRepairTimer.Start();
            }
        }, DispatcherPriority.Loaded);
    }

    private void ApplyNativeInput()
    {
        if (_closed || !IsVisible) return;
        try
        {
            var handle = TryGetPlatformHandle();
            if (handle == null || handle.Handle == IntPtr.Zero)
                throw new InvalidOperationException("The native overlay window is not available.");
            string status;
            if (OperatingSystem.IsLinux())
                status = LinuxOverlayInput.Apply(handle.Handle, handle.HandleDescriptor, _canClickThrough);
            else if (OperatingSystem.IsMacOS())
                status = MacOverlayInput.Apply(handle.Handle, handle.HandleDescriptor, _canClickThrough);
            else if (OperatingSystem.IsWindows())
            {
                MakeWindowClickThroughWindows(_canClickThrough);
                return;
            }
            else throw new PlatformNotSupportedException("Desktop overlays are unavailable on this platform.");
            if (ActualTransparencyLevel == WindowTransparencyLevel.None)
                status += " Transparent background unavailable; enable desktop compositing.";
            OverlayPlatformStatus.Report(Title, status);
        }
        catch (Exception error)
        {
            // Keep enabled settings, but never leave a failed overlay intercepting input.
            _inputRepairTimer.Stop();
            _viewModel.HideOverlayAfterInputFailure();
            OverlayPlatformStatus.Report(Title, $"Hidden: {error.Message} Unlock/relock overlays to retry. See Settings → Overlay details.");
        }
    }

    private void AppearanceSettingsChanged(OverlayAppearance appearance)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_appearanceSubscribed) return;
            _appearance = appearance;
            ApplyAppearance();
        });
    }

    private void ApplyAppearance(bool? locked = null)
    {
        // Keep the move/close controls legible while editing. Applying this to the
        // content also leaves Window.Opacity available for the raid OCR capture.
        ContentObject.Opacity = _appearance.ContentOpacity;
        BackgroundArea.Opacity = _appearance.ResolveBackgroundOpacity(locked ?? _canClickThrough,
            _viewModel.BackgroundLockedOpacity, _viewModel.BackgroundUnLockedOpacity);
        Resources["OverlaySurfaceBrush"] = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#101917"),
            (locked ?? _canClickThrough) ? _appearance.BackgroundOpacity ?? 0.82 : 0.82);
    }

    private void OverlaySettingsChanged(string key)
    {
        if (key == Settings.ShowOverlaysInObsSetting && OperatingSystem.IsWindows())
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_appearanceSubscribed) return;
                _showInObs = Settings.ReadSettingOfType<bool>(Settings.ShowOverlaysInObsSetting);
                ApplyObsCaptureStyle();
            });
            return;
        }
        if (key != OverlayPresentationSettings.Prefix + _viewModel._overlayName) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_appearanceSubscribed) return;
            _viewModel.RefreshPresentation();
            ApplyPresentation();
        });
    }

    private void ApplyPresentation()
    {
        var detailed = _viewModel.SupportsPresentation && _viewModel.PresentationIndex == 1;
        Classes.Set("OverlayDetailed", detailed);
        Classes.Set("OverlayCompact", _viewModel.SupportsPresentation && !detailed);
        PresentationLayout.LayoutTransform = new Avalonia.Media.ScaleTransform(detailed ? 1.15 : 1, detailed ? 1.15 : 1);
    }

    private void ApplyObsCaptureStyle()
    {
        if (TryGetPlatformHandle() is not { } platformHandle) return;

        var hwnd = platformHandle.Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        // OBS skips tool windows. Keep Avalonia's taskbar setting and the other native styles.
        var captureStyle = _showInObs ? style & ~WS_EX_TOOLWINDOW : style | WS_EX_TOOLWINDOW;
        if (captureStyle != style)
            SetWindowLong(hwnd, GWL_EXSTYLE, captureStyle);
    }

    public void ToggleClickThroughCrossPlatform(bool canClickThrough) => ToggleClickThrough(canClickThrough);

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

    private void UpdateState()
    {
        _viewModel.UpdateWindowProperties(new Point(Position.X, Position.Y), new Point(Width, Height));
#if WINDOWS
        var renderScaling = RenderScaling;
#endif
#if MACOS
        var renderScaling = 1;
#endif
#if LINUX
        var renderScaling = 1;
#endif
        _viewModel.UpdateWindowSizeWithScale(new Point(Position.X + (50 * renderScaling), Position.Y + (78* renderScaling)), new Point((Width - 100) * renderScaling, (Height - 78 ) * renderScaling));
        CacheTempPositions(new Point(Position.X, Position.Y), new Point(Width, Height));
    }

    private void DragWindow(object? sender, PointerPressedEventArgs e)
    {
        _isDragging = true;
        e.Pointer.Capture((IInputElement)sender!);
        _startPoint = e.GetPosition(this);
        e.Handled = true;
    }

    private void StopDragWindow(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        e.Pointer.Capture(null);
        Cursor = new Cursor(StandardCursorType.Arrow);
        UpdateState();
        e.Handled = true;
    }

    private void Border_MouseMoved(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
        {
            // Get the current scaling factor to adjust the movement correctly
            var scalingFactor = this.VisualRoot.RenderScaling;

            var currentPosition = e.GetPosition(this);
            var delta = (currentPosition - _startPoint) * scalingFactor; // Adjust for DPI scaling

            // Move the window (or element) by the delta
            var currentPositionInScreen = this.Position;
            this.Position = new PixelPoint(
                currentPositionInScreen.X + (int)delta.X,
                currentPositionInScreen.Y + (int)delta.Y
            );
        }
    }
    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Invoke(_viewModel.CloseButtonClicked);
    }

    private Point startDrag;
    private double initialWidth;
    private double initialHeight;

    private void Drag_Started(object? sender, PointerPressedEventArgs e)
    {
        startDrag = e.GetPosition(this);
        initialWidth = Width;
        initialHeight = Height;
        e.Pointer.Capture((IInputElement)sender!);
        _isDragging = true;
        e.Handled = true;
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
        });
    }

    private void Drag_Stopped(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        _isDragging = false;
        UpdateState();
        e.Handled = true;
    }
}
