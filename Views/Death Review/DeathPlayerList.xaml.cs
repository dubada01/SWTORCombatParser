using SWTORCombatParser.ViewModels.Death_Review;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.Death_Review
{
    /// <summary>
    /// Interaction logic for DeathPlayerList.xaml
    /// </summary>
    public partial class DeathPlayerList : UserControl
    {
        private DeathPlayerListViewModel _viewModel;

        public DeathPlayerList(DeathPlayerListViewModel viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
