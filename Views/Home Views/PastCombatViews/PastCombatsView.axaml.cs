using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.Views.Home_Views.PastCombatViews
{
    /// <summary>
    /// Interaction logic for PastCombatsView.xaml
    /// </summary>
    public partial class PastCombatsView : UserControl
    {
        public PastCombatsView(CombatMonitorViewModel dataContext)
        {
            DataContext = dataContext;
            InitializeComponent();
        }
    }
}
