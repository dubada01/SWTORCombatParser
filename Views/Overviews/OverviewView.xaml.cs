using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Overviews;
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

namespace SWTORCombatParser.Views
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
