using SWTORCombatParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
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
