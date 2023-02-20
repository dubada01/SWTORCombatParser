using System.Windows.Controls;
using SWTORCombatParser.ViewModels.CombatMetaData;

namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for CombatMetaDataView.xaml
    /// </summary>
    public partial class CombatMetaDataView : UserControl
    {
        public CombatMetaDataView(CombatEfffectViewModel dataContext)
        {
            DataContext = dataContext;
            InitializeComponent();
        }
    }
}
