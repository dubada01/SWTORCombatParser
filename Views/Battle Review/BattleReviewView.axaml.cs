using Avalonia.Controls;
using SWTORCombatParser.ViewModels.BattleReview;

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
