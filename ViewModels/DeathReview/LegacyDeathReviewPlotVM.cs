using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.AxisPanels;
using ScottPlot.Plottables;
using SkiaSharp;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Home_View_Models;

namespace SWTORCombatParser.ViewModels.Death_Review;

public class LegacyDeathReviewPlotVM
{
    private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
    private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
    private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
    private List<Entity> _currentPlayers = new List<Entity>();
    private object graphLock = new object();
    private Crosshair _crossHair;
    private SKBitmap _skullImage;
    public event Action<double> XValueSelected = delegate { };

    public LegacyDeathReviewPlotVM()
    {
// Load the original bitmap
        var originalBitmap = SKBitmapFromFile.Load("avares://Orbs/resources/skull_Icon.png");

// Define the target dimensions
        var targetInfo = new SKImageInfo(15, 15);

// Resize the bitmap. You can choose a filter quality (e.g., Low, Medium, or High) depending on the quality and performance you desire.
        var resizedBitmap = originalBitmap.Resize(targetInfo, SKFilterQuality.High);

        if (resizedBitmap == null)
        {
            // Handle the error if resizing failed (e.g., log an error or throw an exception)
            throw new Exception("Failed to resize image.");
        }

// Assign or use resizedBitmap as needed
        _skullImage = resizedBitmap;
        GraphView = new AvaPlot();
        // NOTE: AvaPlot in Avalonia does not support Configuration.Pan/Zoom like the WPF version.
        // To disable these interactions, handle pointer events in the view or override them in your control.
        // GraphView.Configuration.Pan = false;
        // GraphView.Configuration.Zoom = false;

        GraphView.Plot.Axes.Bottom.Label.Text = "Combat Duration (s)";
        GraphView.Plot.Axes.Bottom.Label.FontSize = 12;
        GraphView.Plot.Title("Damage Taken", size: 13);
        
        GraphView.Plot.Axes.Left.Label.Text = "Value";
        GraphView.Plot.Axes.Left.Label.FontSize = 12;
        
        GraphView.Plot.Axes.Right.Label.Text = "HP";
        GraphView.Plot.Axes.Right.Label.FontSize = 12;
        //GraphView.Plot.YAxis2.Ticks(true);

        GraphView.Plot.Legend.Alignment = Alignment.UpperRight; 
        GraphView.Plot.Legend.BackgroundColor = Color.FromHex("#32323232");
        GraphView.Plot.Legend.FontColor = Colors.WhiteSmoke;
        GraphView.Plot.Legend.FontSize = 8;

        InitCrosshair(0);
// Set the data area background color
        GraphView.Plot.DataBackground.Color = Color.FromHex("#640A0A0A");

// Set the figure (entire plot) background color
        GraphView.Plot.FigureBackground.Color = Color.FromHex("#000A0A0A");

// Set the grid line color
        GraphView.Plot.Grid.MajorLineColor = Color.FromHex("#64787878");

// Set tick label color
        GraphView.Plot.Axes.Color(Colors.LightGray);

// Set axis label color
        GraphView.Plot.Axes.Left.Label.ForeColor = Colors.WhiteSmoke;
        GraphView.Plot.Axes.Bottom.Label.ForeColor = Colors.WhiteSmoke;

// Set title label color
        GraphView.Plot.Axes.Title.Label.ForeColor = Colors.WhiteSmoke;

        GraphView.Refresh();
    }
    public AvaPlot GraphView { get; set; }
    
    public ObservableCollection<LegendItemViewModel> GetLegends()
    {
        return new ObservableCollection<LegendItemViewModel>(_seriesToPlot.Select(s => s.Legend));
    }
    
    public void MousePositionUpdated(Point mousePosition)
    {
        lock (graphLock)
        {
            var xVal = GetXValClosestToMouse(mousePosition);
            SetAnnotationPosition(xVal, true);
        }
    }
    public void SetAnnotationPosition(double position, bool fromMouse = false)
    {
        if (_crossHair.X == position) return;
        Dispatcher.UIThread.Invoke(() =>
        {
            _crossHair.X = position;
           // _crossHair.VerticalLine.LineStyle = LineStyle.Solid;
            _crossHair.VerticalLine.Color = Colors.WhiteSmoke;
            _crossHair.VerticalLine.LineWidth = 1;
            //_crossHair.VerticalLine.PositionLabel = true;
            _crossHair.IsVisible = true;
            GraphView.Refresh();
            if (fromMouse)
            {
                XValueSelected(position);
            }
        });
    }
    
    public void Reset()
    {
        lock (graphLock)
        {
            _seriesToPlot.Clear();
            GraphView.Plot.Clear();
            GraphView.Plot.Axes.AutoScale();
        }
        Dispatcher.UIThread.Invoke(() => { GraphView.Refresh(); });
    }
    public void PlotCombat(Combat combatToPlot, List<Entity> viewableEntities, DateTime minVal)
        {
            _currentPlayers = viewableEntities;

            foreach (var entity in _currentPlayers)
            {
                CombatMetaDataSeries series = new CombatMetaDataSeries
                {
                    Name = entity.Name,
                    Color = Palette.GetPalettes().First(p=>p.Name.Contains("Category 10")).GetColor(_currentPlayers.IndexOf(entity)),
                    Type = PlotType.DamageTaken
                };
                _seriesToPlot.Add(series);
                List<ParsedLogEntry> applicableData = GetCorrectData(series.Type, combatToPlot, entity).Where(l => l.TimeStamp > minVal).OrderBy(l => l.TimeStamp).ToList();
                List<ParsedLogEntry> hpData = GetCorrectData(PlotType.HPPercent, combatToPlot, entity).Where(l => l.TimeStamp > minVal).OrderBy(l => l.TimeStamp).ToList();

                if (applicableData == null || applicableData.Count == 0)
                    continue;
                double[] plotXvals;
                double[] plotYvals;
                double[] plotXValRates;
                double[] plotYvaRates;

                plotXvals = PlotMaker.GetPlotXVals(applicableData, combatToPlot.StartTime);
                double[] deathMarkers = hpData
                    .Where(l => l.Effect.EffectId == _7_0LogParsing.DeathCombatId && l.Target == entity &&
                                !(string.IsNullOrEmpty(l.Source.Name))).Select(l => (l.TimeStamp - combatToPlot.StartTime).TotalSeconds).ToArray();
                plotYvals = PlotMaker.GetPlotYVals(applicableData, true);
                plotXValRates = PlotMaker.GetPlotHPXVals(hpData, combatToPlot.StartTime, entity);
                plotYvaRates = PlotMaker.GetHPPercentages(hpData, entity);

                var seriesName = entity.Name;

                series.PointsByCharacter[entity.Name] = GraphView.Plot.Add.Scatter(
                    plotXvals,
                    plotYvals,
                    color: series.Color);
                series.PointsByCharacter[entity.Name].IsVisible = true;
                series.PointsByCharacter[entity.Name].LineStyle = LineStyle.None;
                series.PointsByCharacter[entity.Name].MarkerShape = MarkerShape.FilledCircle;
                series.PointsByCharacter[entity.Name].MarkerSize = 3;
                series.PointsByCharacter[entity.Name].LegendText = seriesName;

                series.LineByCharacter[entity.Name] = GraphView.Plot.Add.ScatterLine(
                    plotXValRates,
                    plotYvaRates,
                    color: series.Color);
                series.LineByCharacter[entity.Name].Axes.YAxis = GraphView.Plot.Axes.Right;
                series.LineByCharacter[entity.Name].LineWidth = 2;
                series.LineByCharacter[entity.Name].IsVisible = true;

                if (deathMarkers.Any())
                {
                    foreach (var marker in deathMarkers)
                    {

                        GraphView.Plot.Add.ImageMarker(new Coordinates(marker, 0), new Image(_skullImage));
                    }
                }
            }
            
            GraphView.Plot.Axes.AutoScale();
            GraphView.Plot.Axes.SetLimits(bottom: 0);
            GraphView.Plot.Axes.SetLimitsY(new AxisLimits(0,0,0,1),GraphView.Plot.Axes.Right);
            InitCrosshair(GraphView.Plot.Axes.GetLimits().Left);
            XValueSelected(GraphView.Plot.Axes.GetLimits().Left);
            Dispatcher.UIThread.Invoke(() =>
            {
                GraphView.Refresh();
            });
        }
    private double GetXValClosestToMouse(Point mousePosition)
    {
        var coords = GraphView.Plot.GetCoordinates((float)mousePosition.X, (float)mousePosition.Y);
        return coords.X;
    }
    private void InitCrosshair(double xVal)
    {
        _crossHair = GraphView.Plot.Add.Crosshair(xVal, 0);
        _crossHair.VerticalLine.Color = Colors.WhiteSmoke;
        _crossHair.VerticalLine.LineWidth = 1;
        _crossHair.VerticalLine.LabelBackgroundColor = Colors.DimGray;
        _crossHair.IsVisible = true;
        _crossHair.HorizontalLine.IsVisible = false;
    }
    private ConcurrentQueue<ParsedLogEntry> GetCorrectData(PlotType type, Combat combatToPlot, Entity selectedParticipant)
    {
        switch (type)
        {
            case PlotType.DamageOutput:
                return combatToPlot.OutgoingDamageLogs[selectedParticipant];
            case PlotType.DamageTaken:
                return combatToPlot.IncomingDamageLogs[selectedParticipant];
            case PlotType.HealingOutput:
                return combatToPlot.OutgoingHealingLogs[selectedParticipant];
            case PlotType.HealingTaken:
                return combatToPlot.IncomingHealingLogs[selectedParticipant];
            case PlotType.SheildedDamageTaken:
                return combatToPlot.ShieldingProvidedLogs[selectedParticipant];
            case PlotType.HPPercent:
                return combatToPlot.GetLogsInvolvingEntity(selectedParticipant);

        }
        return null;
    }
}