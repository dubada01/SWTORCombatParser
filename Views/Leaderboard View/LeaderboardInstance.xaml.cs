using SWTORCombatParser.ViewModels.Leaderboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
