using SWTORCombatParser.ViewModels.Leaderboard;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Leaderboard_View
{
    /// <summary>
    /// Interaction logic for LeaderboardInstance.xaml
    /// </summary>
    public partial class LeaderboardInstance : UserControl
    {
        public LeaderboardInstance(LeaderboardInstanceViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
