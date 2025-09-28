using ScottPlot;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public DeathPlotViewModel()
        {
            _skullImage =
                SKBitmapFromFile.Load("avares://Orbs/resources/skull_Icon.png");
        }

        public AvaPlot GraphView { get; set; }

        public void SetPlot(AvaPlot plot)
        {
            GraphView = plot;
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
        public void PlotCombat(Combat combatToPlot, List<Entity> viewableEntities, string abilityName, Entity objSource)
        {
            _currentPlayers = viewableEntities;
            var pallete = new ScottPlot.Palettes.Nord();
            foreach (var entity in _currentPlayers)
            {
                CombatMetaDataSeries series = new CombatMetaDataSeries
                {
                    Name = entity.Name,
                    Color = pallete.GetColor(_currentPlayers.IndexOf(entity)),
                    Type = PlotType.DamageTaken
                };
                _seriesToPlot.Add(series);
                IOrderedEnumerable<ParsedLogEntry> applicableData = GetCorrectData(series.Type, combatToPlot, entity).Where(l => l.Ability == abilityName && (l.Source.LogId == objSource.LogId || objSource.IsCharacter)).OrderBy(l => l.TimeStamp);
                if (applicableData == null || !applicableData.Any())
                    continue;
                var minTime = applicableData.MinBy(b=>b.TimeStamp).TimeStamp;
                var maxTime = applicableData.MaxBy(b=>b.TimeStamp).TimeStamp;
                List<ParsedLogEntry> hpData = GetCorrectData(PlotType.HPPercent, combatToPlot, entity).Where(l=>l.TimeStamp >= minTime.AddSeconds(-5) && l.TimeStamp <= maxTime.AddSeconds(5)).OrderBy(l => l.TimeStamp).ToList();

                if (applicableData == null || !applicableData.Any())
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
                series.PointsByCharacter[entity.Name].MarkerSize = 5;
                series.PointsByCharacter[entity.Name].LineStyle = LineStyle.None;
                series.PointsByCharacter[entity.Name].LegendText = seriesName;
                series.PointsByCharacter[entity.Name].MarkerShape = MarkerShape.FilledCircle;
                series.PointsByCharacter[entity.Name].IsVisible = true;


                series.LineByCharacter[entity.Name] = GraphView.Plot.Add.ScatterLine(
                    plotXValRates,
                    plotYvaRates,
                    color: series.Color);
                series.LineByCharacter[entity.Name].LineWidth = 1;
                series.LineByCharacter[entity.Name].Axes.YAxis = GraphView.Plot.Axes.Right;
                series.LineByCharacter[entity.Name].IsVisible = true;
                GraphView.Plot.Axes.AutoScale();
                if (deathMarkers.Any())
                {
                    foreach (var marker in deathMarkers)
                    {
                        GraphView.Plot.Add.ImageMarker(new Coordinates(marker,GraphView.Plot.Axes.GetLimits().Top/5),new Image(_skullImage),0.03f);;
                    }
                }
            }
            GraphView.Plot.Axes.AutoScale();
            GraphView.Plot.Axes.SetLimits(bottom: 0);
            GraphView.Plot.Axes.SetLimitsY(new AxisLimits(0,0,0,1),GraphView.Plot.Axes.Right);
            Dispatcher.UIThread.Invoke(GraphView.Refresh);
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
}
