using ScottPlot;
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
using Avalonia.Threading;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using SkiaSharp;
using SWTORCombatParser.Utilities;
using Image = ScottPlot.Image;
using Point = Avalonia.Point;

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
        private SKBitmap _skullImage;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public event Action<double> XValueSelected = delegate { };

        public DeathPlotViewModel()
        {
            _skullImage =
                SKBitmapFromFile.Load("avares://Orbs/resources/skull_Icon.png");
        }

        public AvaPlot GraphView { get; set; }

        public void SetPlot(AvaPlot plot)
        {
            GraphView = plot;
            InitCrosshair(0);
        }
        public ObservableCollection<LegendItemViewModel> GetLegends()
        {
            return new ObservableCollection<LegendItemViewModel>(_seriesToPlot.Select(s => s.Legend));
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
        public void MousePositionUpdated(Point mousePos)
        {
            lock (graphLock)
            {
                var xVal = GetXValClosestToMouse(mousePos);
                SetAnnotationPosition(xVal, true);
            }
        }
        public void SetAnnotationPosition(double position, bool fromMouse = false)
        {
            if (_crossHair.X == position) return;
            Dispatcher.UIThread.Invoke(() =>
            {
                _crossHair.X = position;
                _crossHair.VerticalLine.Color = Colors.WhiteSmoke;
                _crossHair.VerticalLine.LineWidth = 1;
                _crossHair.IsVisible = true;
                if (fromMouse)
                {
                    XValueSelected(position);
                }
                GraphView.Refresh();
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
                    Color = Palette.GetPalettes().First().GetColor(_currentPlayers.IndexOf(entity)),
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
                series.PointsByCharacter[entity.Name].MarkerSize = 3;
                series.PointsByCharacter[entity.Name].LegendText = seriesName;
                series.PointsByCharacter[entity.Name].MarkerShape = MarkerShape.FilledCircle;
                series.PointsByCharacter[entity.Name].IsVisible = true;


                series.LineByCharacter[entity.Name] = GraphView.Plot.Add.ScatterLine(
                    plotXValRates,
                    plotYvaRates,
                    color: series.Color);
                series.LineByCharacter[entity.Name].LineWidth = 2;
                series.LineByCharacter[entity.Name].Axes.YAxis = GraphView.Plot.Axes.Right;
                series.LineByCharacter[entity.Name].IsVisible = true;
                GraphView.Plot.Axes.AutoScale();
                if (deathMarkers.Any())
                {
                    foreach (var marker in deathMarkers)
                    {

                        GraphView.Plot.Add.ImageMarker(new Coordinates(marker,GraphView.Plot.Axes.GetLimits().Top/5),new Image(_skullImage),0.05f);;
                    }
                }
            }
            GraphView.Plot.Axes.AutoScale();
            InitCrosshair(GraphView.Plot.Axes.GetLimits().Left);
            GraphView.Plot.Axes.SetLimits(bottom: 0);
            GraphView.Plot.Axes.SetLimits(right: (combatToPlot.EndTime - combatToPlot.StartTime).TotalSeconds);
            Dispatcher.UIThread.Invoke(GraphView.Refresh);
            XValueSelected(GraphView.Plot.Axes.GetLimits().Left);
        }
        private double GetXValClosestToMouse(Point mousePoint)
        {
            var coord = GraphView.Plot.GetCoordinates((float)mousePoint.X, (float)mousePoint.Y);
            return coord.X;
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
