using Avalonia.Controls;
using SWTORCombatParser.ViewModels.SoftwareLogging;
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
