using SWTORCombatParser.ViewModels.Overlays.AbilityList;

namespace SWTORCombatParser.Views.Overlay.AbilityList
{
    /// <summary>
    /// Interaction logic for AbilityListView.xaml
    /// </summary>
    public partial class AbilityListView : BaseOverlayWindow
    {
        private AbilityListViewModel viewModel;
        public AbilityListView(AbilityListViewModel vm):base(vm)
        {
            viewModel = vm;
            DataContext = vm;
            InitializeComponent();
        }
    }
}
