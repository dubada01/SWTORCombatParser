using SWTORCombatParser.ViewModels.Death_Review;
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ScottPlot;
using ScottPlot.Avalonia;
using SWTORCombatParser.Utilities;
using Image = ScottPlot.Image;


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
        private readonly AvaPlot? _plot;

        public DeathPlot(DeathPlotViewModel viewModel)
        {
            _plotViewModel = viewModel;
            InitializeComponent();
            DataContext = viewModel;
            _plot = this.FindControl<AvaPlot>("PlotArea");
            _plotViewModel.SetPlot(_plot);
            Loaded += OnLoaded;
        }
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            InitPlot();
        }
        
        private void InitPlot()
        {
            _plot.Plot.XLabel("Combat Duration (s)", 12);
            _plot.Plot.YLabel("Damage Taken", 12);
            _plot.Plot.Axes.Right.Label.Text = "HP";
            _plot.Plot.Title("Damage Taken", 13);

            var legend = _plot.Plot.ShowLegend(Alignment.UpperRight);
            legend.BackgroundColor = new Color(50, 50, 50, 50);
            legend.FontColor = Colors.WhiteSmoke;
            legend.FontSize = 10;
            _plot.Plot.DataBackground.Color = new Color(20, 20, 20, 100);
            _plot.Plot.FigureBackground.Color = new Color(10, 10, 10, 255);
            _plot.Plot.Grid.MajorLineColor = new Color(100, 120, 120, 120);
            _plot.Plot.Grid.MinorLineColor = Colors.LightGray;
            _plot.Plot.Axes.Color(Colors.WhiteSmoke);
            var bitmap = SKBitmapFromFile.Load("avares://Orbs/resources/SwtorLogo.png");
            _plot.Plot.FigureBackground.Image = new Image(bitmap);
            _plot.Plot.FigureBackground.ImagePosition = ImagePosition.Center;
            _plot.Plot.PlotControl.UserInputProcessor.Disable();
        }
    }
}
