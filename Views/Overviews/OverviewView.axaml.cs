using Avalonia.Controls;
using SWTORCombatParser.ViewModels.Overviews;

namespace SWTORCombatParser.Views.Overviews
{
    /// <summary>
    /// Interaction logic for HistogramView.xaml
    /// </summary>
    public partial class OverviewView : UserControl
    {
        public OverviewView(OverviewViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
