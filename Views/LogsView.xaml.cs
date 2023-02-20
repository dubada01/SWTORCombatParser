using SWTORCombatParser.ViewModels.SoftwareLogging;
using System.Windows.Controls;

namespace SWTORCombatParser.Views
{
    /// <summary>
    /// Interaction logic for LogsView.xaml
    /// </summary>
    public partial class LogsView : UserControl
    {
        public LogsView(SoftwareLogViewModel dataContext)
        {
            DataContext = dataContext;
            InitializeComponent();
        }
    }
}
