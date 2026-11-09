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
        private bool _initialized;
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
            if (_initialized) return;
            _initialized = true;
            InitPlot();
        }
        private void InitPlot()
        {
            _plot.Plot.XLabel("Combat time", 11);
            _plot.Plot.YLabel("Rate / s", 11);
            InitializeAxisLimits();

            _plot.Plot.HideLegend(); // The interactive metric controls below are the legend.
            _plot.Plot.DataBackground.Color = Color.FromHex("#171A1E");
            _plot.Plot.FigureBackground.Color = Color.FromHex("#171A1E");
            _plot.Plot.Grid.MajorLineColor = Color.FromHex("#30343A");
            _plot.Plot.Grid.MinorLineColor = new Color(0, 0, 0, 0);
            _plot.Plot.Axes.Color(Color.FromHex("#A8B0BA"));
            foreach (var axis in new ScottPlot.IAxis[] { _plot.Plot.Axes.Left, _plot.Plot.Axes.Bottom, _plot.Plot.Axes.Right, _plot.Plot.Axes.Top })
            {
                axis.TickLabelStyle.FontSize = 11;
                axis.MajorTickStyle.Length = 0;
                axis.MinorTickStyle.Length = 0;
                axis.FrameLineStyle.Color = Color.FromHex("#30343A");
            }
            _plot.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
            { TargetTickCount = 8, LabelFormatter = seconds => seconds < 0 ? "" : $"{(int)(seconds / 60)}:{(int)(seconds % 60):00}" };
            _plot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic
            { TargetTickCount = 6, LabelFormatter = amount => Math.Abs(amount) >= 1000000 ? $"{amount / 1000000:0.#}m" : Math.Abs(amount) >= 1000 ? $"{amount / 1000:0.#}k" : $"{amount:0.#}" };
            _plot.Plot.Axes.Right.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic { LabelFormatter = value => $"{value:P0}" };
            _plot.Plot.Axes.Right.TickLabelStyle.IsVisible = _seriesToPlot.First(s => s.Type == PlotType.HPPercent).Legend.Checked;
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
                Dispatcher.UIThread.Invoke(_plotViewModel.RefreshVisibleSeries);
            };
            _seriesToPlot.Add(series);
        }
        private void NotifySizeChanged(object sender, SizeChangedEventArgs e)
        {
            _plotViewModel.UserControlWidth = e.NewSize.Width;
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
