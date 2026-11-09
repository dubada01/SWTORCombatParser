using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using SWTORCombatParser.Utilities.MouseHandler;
using SWTORCombatParser.ViewModels.Overlays.RaidHots;

namespace SWTORCombatParser.Views.Overlay.RaidHOTs;

public partial class RaidFrameOverlay : UserControl
{
    private MouseHookHandler? _mouseHookHandler;
    private bool _manuallyEditing;
    private readonly RaidFrameOverlayViewModel _viewModel;
    public event Action<double, double> AreaClicked = delegate { };

    public RaidFrameOverlay(RaidFrameOverlayViewModel viewModel)
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        InitializeComponent();
        Loaded += (_, _) => { if (_manuallyEditing) SubscribeToClicks(); };
        Unloaded += (_, _) =>
        {
            UnsubscribeFromClicks();
            _viewModel.EndManualAlignment(false);
        };
    }

    public void SetManualEditing(bool enabled)
    {
        _manuallyEditing = enabled;
        if (enabled && IsLoaded) SubscribeToClicks();
        else UnsubscribeFromClicks();
    }

    private void SubscribeToClicks()
    {
        if (_mouseHookHandler != null) return;
        var handler = new MouseHookHandler();
        handler.MouseClicked += GlobalMouseDown;
        try
        {
            handler.SubscribeToClicks();
            _mouseHookHandler = handler;
        }
        catch
        {
            handler.MouseClicked -= GlobalMouseDown;
            handler.UnsubscribeFromClicks();
            throw;
        }
    }

    private void UnsubscribeFromClicks()
    {
        if (_mouseHookHandler == null) return;
        _mouseHookHandler.MouseClicked -= GlobalMouseDown;
        _mouseHookHandler.UnsubscribeFromClicks();
        _mouseHookHandler = null;
    }

    private void GlobalMouseDown(Point position)
    {
        // Windows delivers the hook on the UI thread that installed it. Use the
        // event position, not a later cursor sample after the mouse has moved.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => GlobalMouseDown(position));
            return;
        }
        if (!_manuallyEditing || !TryGetRaidBounds(out var topLeft, out var size, out _)) return;
        var x = (position.X - topLeft.X) / size.X;
        var y = (position.Y - topLeft.Y) / size.Y;
        if (x >= 0 && x < 1 && y >= 0 && y < 1) AreaClicked(x, y);
    }

    // Capture and hit testing share the rendered grid, including its real
    // margins and the overlay monitor's DPI. No title-bar offsets to maintain.
    public bool TryGetRaidBounds(out Point topLeft, out Point size, out double scale)
    {
        topLeft = default;
        size = default;
        scale = 1;
        if (TopLevel.GetTopLevel(this) is not Window { IsVisible: true } window ||
            RaidGrid.Bounds.Width <= 0 || RaidGrid.Bounds.Height <= 0) return false;
        var start = RaidGrid.PointToScreen(new Point(0, 0));
        var end = RaidGrid.PointToScreen(new Point(RaidGrid.Bounds.Width, RaidGrid.Bounds.Height));
        topLeft = new Point(start.X, start.Y);
        size = new Point(end.X - start.X, end.Y - start.Y);
        scale = window.RenderScaling;
#if MACOS
        // screencapture uses desktop points; its Retina bitmap scale is handled by the encoder.
        scale = 1;
#endif
        return size.X >= 1 && size.Y >= 1;
    }
}
