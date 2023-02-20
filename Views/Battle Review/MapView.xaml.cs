using System.Windows.Controls;

namespace SWTORCombatParser.Views.Battle_Review
{
    /// <summary>
    /// Interaction logic for MapView.xaml
    /// </summary>
    public partial class MapView : UserControl
    {
        public MapView(ViewModels.BattleReview.MapViewModel _mapViewModel)
        {
            DataContext = _mapViewModel;
            InitializeComponent();
        }
    }
}
