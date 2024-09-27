using ScottPlot;
using ScottPlot.Plottable;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Avalonia.Threading;
using Path = System.IO.Path;

namespace SWTORCombatParser.ViewModels.Death_Review
{
    public class DeathPlotViewModel
    {
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        private List<Entity> _currentPlayers = new List<Entity>();
        private object graphLock = new object();
        private Crosshair _crossHair;
        private Bitmap _skullImage;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event Action<double> XValueSelected = delegate { };

        public DeathPlotViewModel()
        {
            _skullImage =
                new Bitmap(Path.Combine(Environment.CurrentDirectory, "resources/skull_Icon.png"));
            GraphView = new WpfPlot();
            GraphView.Configuration.Pan = false;
            GraphView.Configuration.Zoom = false;
            GraphView.Plot.XAxis.Label(label: "Combat Duration (s)", size: 12);
            GraphView.Plot.Title("Damage Taken", size: 13);
            GraphView.Plot.YAxis.Label(label: "Value", size: 12);
            GraphView.Plot.YAxis2.Label(label: "HP", size: 12);
            GraphView.Plot.YAxis2.Ticks(true);
            var legend = GraphView.Plot.Legend(location: Alignment.UpperRight);
            legend.FillColor = Color.FromArgb(50, 50, 50, 50);
            legend.FontColor = Color.WhiteSmoke;
            legend.FontSize = 8;
            InitCrosshair(0);
            GraphView.Plot.Style(dataBackground: Color.FromArgb(100, 10, 10, 10),
                figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 120, 120, 120), tick: Color.LightGray, axisLabel: Color.WhiteSmoke, titleLabel: Color.WhiteSmoke);
            GraphView.Refresh();
        }

        public WpfPlot GraphView { get; set; }

        public ObservableCollection<LegendItemViewModel> GetLegends()
        {
            return new ObservableCollection<LegendItemViewModel>(_seriesToPlot.Select(s => s.Legend));
        }
        public void Reset()
        {
            lock (graphLock)
            {
                _seriesToPlot.Clear();
                GraphView.Plot.RenderLock();
                GraphView.Plot.Clear();
                GraphView.Plot.RenderUnlock();
                GraphView.Plot.AxisAuto();
            }
            Dispatcher.UIThread.Invoke(() => { GraphView.Refresh(); });
        }
        public void MousePositionUpdated()
        {
            lock (graphLock)
            {
                var xVal = GetXValClosestToMouse();
                SetAnnotationPosition(xVal, true);
            }
        }
        public void SetAnnotationPosition(double position, bool fromMouse = false)
        {
            if (_crossHair.X == position) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                _crossHair.X = position;
                _crossHair.VerticalLine.LineStyle = LineStyle.Solid;
                _crossHair.VerticalLine.Color = Color.WhiteSmoke;
                _crossHair.VerticalLine.LineWidth = 1;
                _crossHair.VerticalLine.PositionLabel = true;
                _crossHair.IsVisible = true;
                GraphView.Render();
                if (fromMouse)
                {
                    XValueSelected(position);
                }
            });
        }
        public void PlotCombat(Combat combatToPlot, List<Entity> viewableEntities, DateTime minVal)
        {
            _currentPlayers = viewableEntities;

            foreach (var entity in _currentPlayers)
            {
                CombatMetaDataSeries series = new CombatMetaDataSeries
                {
                    Name = entity.Name,
                    Color = Palette.Category10.GetColor(_currentPlayers.IndexOf(entity)),
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

                series.PointsByCharacter[entity.Name] = GraphView.Plot.AddScatter(
                    plotXvals,
                    plotYvals,
                    lineStyle: LineStyle.None,
                    markerShape: MarkerShape.filledCircle,
                    label: seriesName,
                    color: series.Color,
                    markerSize: 3);
                series.PointsByCharacter[entity.Name].IsVisible = true;


                series.LineByCharacter[entity.Name] = GraphView.Plot.AddScatter(
                    plotXValRates,
                    plotYvaRates,
                    lineStyle: LineStyle.Solid,
                    markerShape: MarkerShape.none,
                    color: series.Color,
                    lineWidth: 2);
                series.LineByCharacter[entity.Name].YAxisIndex = 1;
                series.LineByCharacter[entity.Name].IsVisible = true;

                if (deathMarkers.Any())
                {
                    foreach (var marker in deathMarkers)
                    {

                        GraphView.Plot.AddImage(new Bitmap(_skullImage, 15, 15)
                            , marker,
                            0, anchor: Alignment.LowerCenter);
                    }
                }
            }
            GraphView.Plot.AxisAuto();
            InitCrosshair(GraphView.Plot.GetAxisLimits(0).XMin);
            GraphView.Plot.SetAxisLimits(yMin: 0, yAxisIndex: 1);
            GraphView.Plot.SetAxisLimits(xMax: (combatToPlot.EndTime - combatToPlot.StartTime).TotalSeconds);
            Dispatcher.UIThread.Invoke(GraphView.Refresh);
            XValueSelected(GraphView.Plot.GetAxisLimits(0).XMin);
        }
        private double GetXValClosestToMouse()
        {
            (double mouseCoordX, double mouseCoordY) = GraphView.GetMouseCoordinates();
            return mouseCoordX;
        }
        private void InitCrosshair(double xVal)
        {
            _crossHair = GraphView.Plot.AddCrosshair(xVal, 0);
            _crossHair.VerticalLine.LineStyle = LineStyle.Solid;
            _crossHair.VerticalLine.Color = Color.WhiteSmoke;
            _crossHair.VerticalLine.LineWidth = 1;
            _crossHair.VerticalLine.PositionLabel = true;
            _crossHair.VerticalLine.PositionLabelBackground = Color.DimGray;
            _crossHair.IsVisible = true;
            _crossHair.HorizontalLine.IsVisible = false;
        }
        private List<ParsedLogEntry> GetCorrectData(PlotType type, Combat combatToPlot, Entity selectedParticipant)
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
                    return combatToPlot.GetLogsInvolvingEntity(selectedParticipant).ToList();

            }
            return null;
        }
    }
}
