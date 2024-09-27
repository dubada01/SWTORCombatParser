using SWTORCombatParser.ViewModels.DataGrid;
using System.Windows.Controls;

namespace SWTORCombatParser.Views.DataGrid_Views
{
    /// <summary>
    /// Interaction logic for DataGridView.xaml
    /// </summary>
    public partial class DataGridView : UserControl
    {
        public DataGridView(DataGridViewModel vm)
        {
            DataContext = vm;
            InitializeComponent();
        }
    }
}
