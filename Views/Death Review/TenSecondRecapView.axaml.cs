using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SWTORCombatParser.ViewModels.Death_Review;

namespace SWTORCombatParser.Views.Death_Review;

public partial class TenSecondRecapView : UserControl
{
    public TenSecondRecapView(TenSecondRecapViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}