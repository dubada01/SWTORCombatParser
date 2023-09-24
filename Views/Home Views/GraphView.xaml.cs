using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for GraphView.xaml
    /// </summary>
    public partial class GraphView : UserControl
    {
        private DateTime _lastAnnotationUpdateTime;
        private double _annotationUpdatePeriodMS = 50;
        private PlotViewModel _plotViewModel;
        public GraphView(PlotViewModel dataContext)
        {
            DataContext = dataContext;
            _plotViewModel = dataContext;
            InitializeComponent();
        }
        private void GridView_MouseMove(object sender, MouseEventArgs e)
        {
            if ((DateTime.Now - _lastAnnotationUpdateTime).TotalMilliseconds > _annotationUpdatePeriodMS)
            {
                _lastAnnotationUpdateTime = DateTime.Now;
            }
            else
                return;

            _plotViewModel.MousePositionUpdated();
        }
    }
}
