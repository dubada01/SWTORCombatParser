using Avalonia.Controls;
using SWTORCombatParser.ViewModels.CombatMetaData;

namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for CombatMetaDataView.xaml
    /// </summary>
    public partial class EffectsView : UserControl
    {
        public EffectsView(CombatEfffectViewModel dataContext)
        {
            DataContext = dataContext;
            InitializeComponent();
        }
    }
}
