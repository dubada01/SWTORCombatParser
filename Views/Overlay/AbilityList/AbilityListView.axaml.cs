using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overlays.AbilityList;

namespace SWTORCombatParser.Views.Overlay.AbilityList
{
    /// <summary>
    /// Interaction logic for AbilityListView.xaml
    /// </summary>
    public partial class AbilityListView : UserControl
    {
        private AbilityListViewModel viewModel;
        public AbilityListView(AbilityListViewModel vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
        }
    }
}
