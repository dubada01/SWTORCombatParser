using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Overlays;

namespace SWTORCombatParser.Views.Overlay;

public partial class OverlayOptionView : UserControl
{
    public OverlayOptionView() => InitializeComponent();
    private void Button_PreviewMouseRightButtonUp(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Right) return;
        e.Handled = true;
        OpenColorPicker();
    }
    private void ChangeColor(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        OpenColorPicker();
    }
    private async void OpenColorPicker()
    {
        if (DataContext is not OverlayOptionViewModel metric || TopLevel.GetTopLevel(this) is not Window owner) return;
        var model = new MetricColorPickerViewModel(metric.Type);
        var view = new MetricColorPickerWindow(model) { WindowStartupLocation = WindowStartupLocation.CenterOwner };
        model.CloseRequested += view.Close;
        await view.ShowDialog(owner);
    }
}
