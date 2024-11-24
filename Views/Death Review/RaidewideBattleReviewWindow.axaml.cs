using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SWTORCombatParser.ViewModels.Death_Review;

namespace SWTORCombatParser.Views.Death_Review;

public partial class RaidwideBattleReviewWindow : Window
{
    public RaidwideBattleReviewWindow(RaidwideBattleReviewViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}