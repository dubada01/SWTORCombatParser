using SWTORCombatParser.ViewModels.Challenges;
using Avalonia.Controls;

namespace SWTORCombatParser.Views.Challenges
{
    /// <summary>
    /// Interaction logic for ChallengeWindow.xaml
    /// </summary>
    public partial class ChallengeWindow : UserControl
    {
        public ChallengeWindow(ChallengeWindowViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
