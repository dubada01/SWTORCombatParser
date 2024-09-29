using Avalonia.Controls;
using SWTORCombatParser.ViewModels.DataGrid;

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
