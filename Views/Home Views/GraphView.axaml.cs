using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ScottPlot;
using ScottPlot.Avalonia;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Utilities;
using Colors = Avalonia.Media.Colors;
using Image = ScottPlot.Image;


namespace SWTORCombatParser.Views.Home_Views
{
    /// <summary>
    /// Interaction logic for GraphView.xaml
    /// </summary>
    public partial class GraphView : UserControl
    {
        // Store previous axis limits
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        double previousXMin, previousXMax, previousYMin, previousYMax;
        private DateTime _lastAnnotationUpdateTime;
        private double _annotationUpdatePeriodMS = 50;
        private PlotViewModel _plotViewModel;
        private AvaPlot _plot;
        public GraphView(PlotViewModel dataContext)
        {
            DataContext = dataContext;
            _plotViewModel = dataContext;
            InitializeComponent();
            ConfigureSeries(Enum.GetValues(typeof(PlotType)).Cast<PlotType>().ToList());
            this.SizeChanged += NotifySizeChanged;
            _plot = this.FindControl<AvaPlot>("GridView");
            _plotViewModel.SetPlotForViewModel(_plot);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            InitPlot();
        }
        private void InitPlot()
        {
            _plot.Plot.XLabel("Combat Duration (s)", 12);
            _plot.Plot.YLabel("Value", 12);
            InitializeAxisLimits();

            var legend = _plot.Plot.ShowLegend(Alignment.UpperRight);
            legend.BackgroundColor = new Color(50, 50, 50, 50);
            legend.FontColor = Color.FromARGB(Colors.WhiteSmoke.ToUInt32());
            legend.FontSize = 10;
            _plot.Plot.DataBackground.Color = new Color(20, 20, 20, 100);
            _plot.Plot.FigureBackground.Color = new Color(10, 10, 10, 255);
            _plot.Plot.Grid.MajorLineColor = new Color(100, 120, 120, 120);
            _plot.Plot.Grid.MinorLineColor = Color.FromARGB(Colors.LightGray.ToUInt32());
            _plot.Plot.Axes.Color(Color.FromARGB(Colors.WhiteSmoke.ToUInt32()));
            _plot.Plot.FigureBackground.Image = new Image(SKBitmapFromFile.Load("avares://Orbs/resources/SwtorLogo.png"));
            _plot.Plot.FigureBackground.ImagePosition = ImagePosition.Center;
        }
        // Initialize with current axis limits
        private void InitializeAxisLimits()
        {
            var limits = _plot.Plot.Axes.GetLimits();
            previousXMin = limits.Left;
            previousXMax = limits.Right;
            previousYMin = limits.Top;
            previousYMax = limits.Bottom;
        }
        private void ConfigureSeries(List<PlotType> seriesToPlot)
        {
            foreach (var plotType in seriesToPlot)
            {
                switch (plotType)
                {
                    case PlotType.DamageOutput:
                        AddSeries(plotType, "DPS", Color.FromARGB(Colors.LightCoral.ToUInt32()), true);
                        break;
                    case PlotType.DamageTaken:
                        AddSeries(plotType, "DTPS", Color.FromARGB(Colors.Peru.ToUInt32()), true);
                        break;
                    case PlotType.SheildedDamageTaken:
                        AddSeries(plotType, "Absorb", Color.FromARGB(Colors.WhiteSmoke.ToUInt32()));
                        break;
                    case PlotType.HealingOutput:
                        AddSeries(plotType, "HPS", Color.FromARGB(Colors.MediumAquamarine.ToUInt32()), true);
                        break;
                    case PlotType.HealingTaken:
                        AddSeries(plotType, "HRPS", Color.FromARGB(Colors.LightSkyBlue.ToUInt32()), true);
                        break;
                    case PlotType.HPPercent:
                        AddSeries(plotType, "HP", Color.FromARGB(Colors.LightGoldenrodYellow.ToUInt32()), false, false);
                        break;
                    default:
                        break;
                }
            }

            _plotViewModel.SetSeries(_seriesToPlot);
        }
        private void AddSeries(PlotType type, string name, Color color, bool hasEffective = false, bool selectedByDefault = true)
        {
            var series = new CombatMetaDataSeries();
            series.Type = type;
            series.Name = name;
            series.Color = color;
            var legend = new LegendItemViewModel();
            legend.Checked = selectedByDefault;
            legend.Name = series.Name;
            legend.Color = series.Color;
            legend.LegenedToggled += series.LegenedToggled;
            legend.HasEffective = hasEffective;
            series.Legend = legend;
            series.TriggerRender += (toggleState) =>
            {
                if (toggleState && type == PlotType.HPPercent)
                {
                    _plot.Plot.XLabel("Health", size: 12);
                }
                if (!toggleState && type == PlotType.HPPercent)
                {
                    _plot.Plot.Axes.Right.Label.Text = "";
                    _plot.Plot.Axes.Right.TickLabelStyle.IsVisible = false;
                }
                Dispatcher.UIThread.Invoke(() =>
                {
                    _plot.Plot.Axes.AutoScale();
                    _plot.Plot.Axes.SetLimits(bottom:0);
                    _plot.Refresh();
                });
            };
            _seriesToPlot.Add(series);
        }
        private void NotifySizeChanged(object sender, SizeChangedEventArgs e)
        {
            _plotViewModel.UserControlWidth = this.Width;
        }

        private void GridView_MouseMove(object sender, PointerEventArgs e)
        {
            if ((DateTime.Now - _lastAnnotationUpdateTime).TotalMilliseconds > _annotationUpdatePeriodMS)
            {
                _lastAnnotationUpdateTime = DateTime.Now;
            }
            else
                return;

            _plotViewModel.MousePositionUpdated(e.GetPosition(GridView));
            TryUpdateAxes();
        }

        private void TryUpdateAxes()
        {
            var currentLimits = _plot.Plot.Axes.GetLimits(); // Adjusted method
            var moveTol = 1;
            // Check if the X or Y limits have changed (zoom/pan detection)
            if (Math.Abs(currentLimits.Left - previousXMin) > moveTol || Math.Abs(currentLimits.Right - previousXMax) > moveTol ||
                Math.Abs(currentLimits.Top - previousYMin) > moveTol || Math.Abs(currentLimits.Bottom - previousYMax) > moveTol)
            {
                // Zoom or pan event detected
                HandleZoomOrPan(currentLimits);

                // Update the previous axis limits
                previousXMin = currentLimits.Left;
                previousXMax = currentLimits.Right;
                previousYMin = currentLimits.Top;
                previousYMax = currentLimits.Bottom;
            }
        }
        // Handle the zoom/pan action
        void HandleZoomOrPan(AxisLimits limits)
        {
            _plotViewModel.UpdatePlotAxis(limits);
        }
    }
}
