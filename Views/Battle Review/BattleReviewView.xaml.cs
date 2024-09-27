using SWTORCombatParser.ViewModels.BattleReview;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Battle_Review
{
    /// <summary>
    /// Interaction logic for BattleReviewView.xaml
    /// </summary>
    public partial class BattleReviewView : UserControl
    {
        public BattleReviewView(BattleReviewViewModel _reviewViewModel)
        {
            DataContext = _reviewViewModel;
            InitializeComponent();
        }
    }
}
