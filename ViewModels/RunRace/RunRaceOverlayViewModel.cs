using System;
using Avalonia;
using SWTORCombatParser.Model.RunRace;
using SWTORCombatParser.Views.RunRace;

namespace SWTORCombatParser.ViewModels.RunRace;

public sealed class RunRaceOverlayViewModel : BaseOverlayViewModel, IDisposable
{
    public override bool SupportsPresentation => true;
    public RunRaceViewModel Race { get; }
    public override bool ShouldBeVisible => Race.HasRun;
    public override string OverlayDisplayName => "Run Race";
    public RunRaceOverlayViewModel(RunRaceService service, string overlayKey = "Timeline") : base(overlayKey)
    {
        Race = new RunRaceViewModel(service);
        Race.DisplayUpdated += UpdateVisibility;
        MainContent = new RunRaceOverlayView { DataContext = this };
        var placeholderSize = OverlayScaledSize == new Point(250, 100) || _overlayWindow.Width == 250 && _overlayWindow.Height == 100;
        _overlayWindow.MinWidth = 380;
        _overlayWindow.MinHeight = 150;
        // Upgrade only the generic first-run placeholder; keep existing custom dimensions and placement.
        if (placeholderSize)
        {
            UpdateWindowProperties(new Point(_overlayWindow.Position.X, _overlayWindow.Position.Y), new Point(640, 300));
            // Refresh the base window's cached opening size as well as its current bounds.
            InitPositionAndSize();
        }
    }

    public void Dispose()
    {
        Race.DisplayUpdated -= UpdateVisibility;
        Race.Dispose();
        UnsubscribeFromLogEvents();
        _overlayWindow?.Close();
    }
}
