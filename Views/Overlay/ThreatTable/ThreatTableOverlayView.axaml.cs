using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SWTORCombatParser.ViewModels.Overlays.ThreatTable;

namespace SWTORCombatParser.Views.Overlay.ThreatTable;

public partial class ThreatTableOverlayView : UserControl
{
    private readonly ThreatTableOverlayViewModel _viewModel;

    public ThreatTableOverlayView(ThreatTableOverlayViewModel overlayViewModel)
    {
        _viewModel = overlayViewModel;
        DataContext = _viewModel;
        InitializeComponent();
    }
}