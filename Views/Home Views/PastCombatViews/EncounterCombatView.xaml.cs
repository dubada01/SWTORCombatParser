using SWTORCombatParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SWTORCombatParser.Views.PastCombatViews
{
    /// <summary>
    /// Interaction logic for EncounterCombat.xaml
    /// </summary>
    public partial class EncounterCombatView : UserControl
    {
        public EncounterCombatView()
        {
            InitializeComponent();
        }
        private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var viewModel = DataContext as EncounterCombat;

            viewModel.ToggleCombatVisibility();

        }
    }
}
