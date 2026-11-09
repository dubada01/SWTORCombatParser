using Avalonia.Controls;
using Avalonia.Interactivity;
using SWTORCombatParser.ViewModels.Overlays;
using SWTORCombatParser.Views.Overlay.RaidHOTs;

namespace SWTORCombatParser.Views.Overlay;

public partial class OverlayView : Window
{
    private readonly OverlayViewModel _viewModel;
    private Window? _raidSetup;
    public OverlayView(OverlayViewModel viewmodel)
    {
        _viewModel = viewmodel;
        DataContext = viewmodel;
        InitializeComponent();
        Closed += (_, _) => _raidSetup?.Close();
    }
    private void Done(object? sender, RoutedEventArgs e) => Close();
    private void OpenRaidFrameSetup(object? sender, RoutedEventArgs e)
    {
        if (_raidSetup != null) { _raidSetup.Activate(); return; }
        _raidSetup = new Window
        {
            Title = "Raid frame setup", Width = 440, Height = 330, MinWidth = 380, MinHeight = 270,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new RaidHOTsSteup { DataContext = _viewModel.RaidFrameConfig }
        };
        _raidSetup.Classes.Add("OrbsDialog");
        _raidSetup.Closed += (_, _) => _raidSetup = null;
        // Modeless so the user can position the raid grid and return to SWTOR.
        _raidSetup.Show(this);
    }
}
