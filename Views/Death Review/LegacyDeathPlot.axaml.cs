using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ScottPlot;
using ScottPlot.Avalonia;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Death_Review;
using Image = ScottPlot.Image;

namespace SWTORCombatParser.Views.Death_Review
{
    public partial class LegacyDeathPlot : UserControl
    {
        private readonly LegacyDeathReviewPlotVM _viewModel;
        private DateTime _lastAnnotationUpdateTime;
        private double _annotationUpdatePeriodMS = 100;

        public LegacyDeathPlot(LegacyDeathReviewPlotVM viewModel)
        {
            _viewModel = viewModel;
            InitializeComponent();
            this.FindControl<AvaPlot>("PlotArea");
            _viewModel.GraphView = PlotArea;
            Loaded += OnLoaded;
        }

        private void GridView_MouseMove(object? sender, PointerEventArgs e)
        {
            if ((DateTime.Now - _lastAnnotationUpdateTime).TotalMilliseconds > _annotationUpdatePeriodMS)
            {
                _lastAnnotationUpdateTime = DateTime.Now;
            }
            else
                return;

            _viewModel.MousePositionUpdated(e.GetPosition(PlotArea));
        }
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            InitPlot();
        }
        
        private void InitPlot()
        {
            PlotArea.Plot.XLabel("Combat Duration (s)", 12);
            PlotArea.Plot.YLabel("Damage Taken", 12);
            PlotArea.Plot.Axes.Right.Label.Text = "HP";
            PlotArea.Plot.Title("Damage Taken", 13);

            var legend = PlotArea.Plot.ShowLegend(Alignment.UpperRight);
            legend.BackgroundColor = new Color(50, 50, 50, 50);
            legend.FontColor = Colors.WhiteSmoke;
            legend.FontSize = 10;
            PlotArea.Plot.DataBackground.Color = new Color(20, 20, 20, 100);
            PlotArea.Plot.FigureBackground.Color = new Color(10, 10, 10, 255);
            PlotArea.Plot.Grid.MajorLineColor = new Color(100, 120, 120, 120);
            PlotArea.Plot.Grid.MinorLineColor = Colors.LightGray;
            PlotArea.Plot.Axes.Color(Colors.WhiteSmoke);
            var bitmap = SKBitmapFromFile.Load("avares://Orbs/resources/SwtorLogo.png");
            PlotArea.Plot.FigureBackground.Image = new Image(bitmap);
            PlotArea.Plot.FigureBackground.ImagePosition = ImagePosition.Center;
            PlotArea.Plot.PlotControl.UserInputProcessor.Disable();
        }
    }
}