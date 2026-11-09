using Avalonia.Controls;
using SWTORCombatParser.ViewModels.BattleReview;
using Avalonia.Input;

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
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
                { SearchBox.Focus(); SearchBox.SelectAll(); e.Handled = true; }
            };
        }
        private void SearchKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not BattleReviewViewModel model) return;
            if (e.Key == Key.Enter) { model.ApplySearch(); e.Handled = true; }
            if (e.Key == Key.Escape) { model.LogFilter = ""; model.ApplySearch(); e.Handled = true; }
        }
    }
}
