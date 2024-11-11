using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Death_Review;


namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for DeathReviewPage.xaml
    /// </summary>
    public partial class DeathReviewPage : Window
    {
        public DeathReviewPage(DeathReviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
