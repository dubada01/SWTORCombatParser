using SWTORCombatParser.ViewModels.Death_Review;
using System;
using Avalonia.Controls;
using Avalonia.Input;


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

        private void GridView_MouseMove(object sender, PointerEventArgs e)
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

        private void GridView_MouseDown(object sender, PointerPressedEventArgs e)
        {
            _mouseDown = true;
        }

        private void GridView_MouseUp(object sender, PointerReleasedEventArgs e)
        {
            _mouseDown = false;
        }
    }
}
