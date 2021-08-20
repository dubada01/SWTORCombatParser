using SWTORCombatParser.ViewModels.Overlays;
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
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    public partial class OverlayView : UserControl
    {
        public OverlayView(OverlayViewModel viewmodel)
        {
            DataContext = viewmodel;
            InitializeComponent();
        }
    }
}
