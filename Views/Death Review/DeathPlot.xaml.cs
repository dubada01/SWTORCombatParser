using SWTORCombatParser.ViewModels.Death_Review;
using SWTORCombatParser.ViewModels.Home_View_Models;
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

namespace SWTORCombatParser.Views.Death_Review
{
    /// <summary>
    /// Interaction logic for DeathPlot.xaml
    /// </summary>
    public partial class DeathPlot : UserControl
    {
        private DateTime _lastAnnotationUpdateTime;
        private double _annotationUpdatePeriodMS = 50;
        private DeathPlotViewModel _plotViewModel;
        private bool _mouseDown;
        public DeathPlot(DeathPlotViewModel viewModel)
        {
            _plotViewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
        }

        private void GridView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_mouseDown)
            { return; }
            if ((DateTime.Now - _lastAnnotationUpdateTime).TotalMilliseconds > _annotationUpdatePeriodMS)
            {
                _lastAnnotationUpdateTime = DateTime.Now;
            }
            else
                return;

            _plotViewModel.MousePositionUpdated();
        }

        private void GridView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _mouseDown = true;
        }

        private void GridView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _mouseDown= false;
        }
    }
}
