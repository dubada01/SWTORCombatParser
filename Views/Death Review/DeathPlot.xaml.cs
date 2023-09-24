using SWTORCombatParser.ViewModels.Death_Review;
using System;
using System.Windows.Controls;
using System.Windows.Input;

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
            _mouseDown = false;
        }
    }
}
