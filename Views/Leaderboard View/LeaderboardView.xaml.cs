using SWTORCombatParser.ViewModels.Leaderboard;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Leaderboard_View
{
    /// <summary>
    /// Interaction logic for LeaderboardView.xaml
    /// </summary>
    public partial class LeaderboardView : UserControl
    {
        public LeaderboardView(LeaderboardViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
