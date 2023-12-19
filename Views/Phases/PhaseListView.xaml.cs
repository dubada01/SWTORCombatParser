using SWTORCombatParser.ViewModels.Phases;
using System.Windows;

namespace SWTORCombatParser.Views.Phases
{
    /// <summary>
    /// Interaction logic for PhaseListView.xaml
    /// </summary>
    public partial class PhaseListView : Window
    {
        public PhaseListView(PhaseListViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
