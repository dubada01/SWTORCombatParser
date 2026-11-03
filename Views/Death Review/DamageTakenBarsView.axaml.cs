using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SWTORCombatParser.ViewModels.Death_Review;

namespace SWTORCombatParser.Views.Death_Review;

public partial class DamageTakenBarsView : UserControl
{
    private DamageTakenBarsViewModel _viewModel;
    public DamageTakenBarsView(DamageTakenBarsViewModel viewModel)
    {
        DataContext = viewModel;
        _viewModel = viewModel;
        InitializeComponent();
    }

    private void InputElement_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control)
        {
            // Get the DataContext of the UI element that triggered the event
            var barInfo = control.DataContext as BarInfo;

            // Perform your logic with barInfo
            if (barInfo != null)
            {
                _viewModel.BarSelected(barInfo);
            }
        }
    }
}