using ScottPlot;
using ScottPlot.Plottable;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace SWTORCombatParser.Plotting
{
    public enum PlotType
    {
        DamageOutput,
        DamageTaken,
        HealingOutput,
        HealingTaken,
        SheildedDamageTaken,
        HPPercent
    }
    public class PlotViewModel
    {
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        private List<Combat> _currentCombats = new List<Combat>();
        private CombatMetaDataViewModel _combatMetaDataViewModel;
        private object graphLock = new object();
        private Entity _currentParticipant;
        public PlotViewModel()
        {
            _combatMetaDataViewModel = new CombatMetaDataViewModel();
            _combatMetaDataViewModel.OnNewParticipantSelected += SeletedParticipant;
            _combatMetaDataViewModel.OnEffectSelected += HighlightEffect;
            _combatMetaDataViewModel.OnEffectsCleared += ResetEffectVisuals;
            CombatMetaDataView = new CombatMetaDataView(_combatMetaDataViewModel);

            GraphView = new WpfPlot();
            GraphView.Plot.XLabel("Combat Duration (s)");
            GraphView.Plot.YLabel("Ammount");
            
            GraphView.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 2, title: "Rate");
            GraphView.AxesChanged += UpdatePlotAxis;
            var legend = GraphView.Plot.Legend(location: Alignment.UpperRight);
            legend.FillColor = Color.FromArgb(50, 50, 50, 50);
            legend.FontColor = Color.WhiteSmoke;
            legend.FontSize = 15;
            ConfigureSeries(Enum.GetValues(typeof(PlotType)).Cast<PlotType>().ToList());
            LegendItems = GetLegends();
            GraphView.Plot.Style(dataBackground: Color.FromArgb(150, 10, 10, 10), figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 40, 40, 40));
            GraphView.Plot.AddPoint(0, 0, color: Color.Transparent);
        }

        private void SeletedParticipant(Entity obj)
        {
            if (_currentParticipant == obj)
                return;
            _currentParticipant = obj;
            lock (graphLock)
            {
                GraphView.Plot.Clear();
                GraphView.Plot.AxisAuto();
                GraphView.Plot.SetAxisLimits(xMin: 0);
                if (_currentCombats.Count == 0)
                    return;

                PlotCombat(_currentCombats[0], obj);

            }
        }

        public CombatMetaDataView CombatMetaDataView { get; set; }
        public WpfPlot GraphView { get; set; }
        public ObservableCollection<LegendItemViewModel> LegendItems { get; set; }
        public void ConfigureSeries(List<PlotType> seriesToPlot)
        {
            foreach (var plotType in seriesToPlot)
            {
                switch (plotType)
                {
                    case PlotType.DamageOutput:
                        AddSeries(plotType, "Damage Output", Color.IndianRed,true);
                        break;
                    case PlotType.DamageTaken:
                        AddSeries(plotType, "Damage Incoming", Color.Peru, true);
                        break;
                    case PlotType.SheildedDamageTaken:
                        AddSeries(plotType, "Sheilding", Color.WhiteSmoke);
                        break;
                    case PlotType.HealingOutput:
                        AddSeries(plotType, "Heal Output", Color.LimeGreen, true);
                        break;
                    case PlotType.HealingTaken:
                        AddSeries(plotType, "Heal Incoming", Color.CornflowerBlue, true);
                        break;
                    case PlotType.HPPercent:
                        AddSeries(plotType, "Health Percentage", Color.LightGoldenrodYellow,false,false);
                        break;
                    default:
                        Trace.WriteLine("Invalid Series");
                        break;
                }
            }
        }
        public void HighlightEffect(List<CombatModifier> obj)
        {
            lock (graphLock)
            {
                ResetEffectVisuals();
                foreach (var effect in obj)
                {
                    var startTime = _currentCombats.OrderBy(c => c.StartTime).ToList()[0].StartTime;
                    var endTime = _currentCombats.OrderByDescending(c => c.EndTime).ToList()[0].EndTime;
                    var maxDuration = (endTime - startTime).TotalSeconds;
                    var effectStart = (effect.StartTime - startTime).TotalSeconds;
                    var effectEnd = (effect.StopTime - startTime).TotalSeconds;
                    GraphView.Plot.AddHorizontalSpan(Math.Max(effectStart, 0), Math.Min(effectEnd, maxDuration), color: Color.FromArgb(50, Color.LightYellow));
                }
            }
        }

        internal void UpdateParticipants(List<Entity> obj)
        {
            _combatMetaDataViewModel.AvailableParticipants = obj; 
        }

        public void UpdateLivePlot(Combat updatedCombat)
        {
            lock (graphLock)
            {
                ResetEffectVisuals();
                var staleCombat = _currentCombats.FirstOrDefault(c => c.StartTime == updatedCombat.StartTime);
                if (staleCombat != null)
                {
                    RemoveCombatPlot(staleCombat);
                }

                _currentCombats.Add(updatedCombat);
                PlotCombat(updatedCombat, updatedCombat.CharacterParticipants.First());
            }
        }
        public void AddCombatPlot(Combat combatToPlot)
        {
            lock (graphLock)
            {
                ResetEffectVisuals();
                _currentCombats.Add(combatToPlot);
                PlotCombat(combatToPlot, combatToPlot.CharacterParticipants.First());
            }
        }
        public void RemoveCombatPlot(Combat combatToRemove)
        {
            lock (graphLock)
            {
                ResetEffectVisuals();
                if (!_currentCombats.Any(c => c.StartTime == combatToRemove.StartTime))
                    return;
                _currentCombats.Remove(_currentCombats.First(c => c.StartTime == combatToRemove.StartTime));

                foreach (var series in _seriesToPlot)
                {
                    GraphView.Plot.RenderLock();
                    if (series.Points.ContainsKey(combatToRemove.StartTime))
                    {
                        series.Points.Remove(combatToRemove.StartTime);
                        series.Line.Remove(combatToRemove.StartTime);
                        series.Tooltip.Remove(combatToRemove.StartTime);
                    }
                    if (series.Legend.HasEffective && series.EffectivePoints.ContainsKey(combatToRemove.StartTime))
                    {
                        series.Tooltip.Remove(combatToRemove.StartTime);
                        series.EffectivePoints.Remove(combatToRemove.StartTime);
                        series.EffectiveLine.Remove(combatToRemove.StartTime);
                    }
                    GraphView.Plot.RenderUnlock();
                }

                GraphView.Plot.RenderLock();
                GraphView.Plot.Clear();
                GraphView.Plot.RenderUnlock();

                foreach (var remainingCombat in _currentCombats)
                {
                    PlotCombat(remainingCombat, remainingCombat.CharacterParticipants.First());
                }

                GraphView.Plot.AxisAuto();
                GraphView.Plot.SetAxisLimits(xMin: 0);
            }
        }
        public ObservableCollection<LegendItemViewModel> GetLegends()
        {
            return new ObservableCollection<LegendItemViewModel>(_seriesToPlot.Select(s => s.Legend));
        }
        public void Reset()
        {
            lock (graphLock)
            {
                _currentCombats.Clear();
                _combatMetaDataViewModel.Reset();
                GraphView.Plot.Clear();
                GraphView.Plot.AxisAuto();
                GraphView.Plot.SetAxisLimits(xMin: 0);
            }
        }
        public void MousePositionUpdated()
        {
            lock (graphLock)
            {
                foreach (var plot in _seriesToPlot)
                {
                    if (plot.EffectiveTooltip != null)
                        plot.EffectiveTooltip.Values.ToList().ForEach(v => v.IsVisible = false);
                    if (plot.Tooltip != null)
                        plot.Tooltip.Values.ToList().ForEach(v => v.IsVisible = false);
                    foreach (var combat in _currentCombats)
                    {
                        if (plot.Points.Count > 0 && plot.Points.ContainsKey(combat.StartTime) && GraphView.Plot.GetPlottables().Contains(plot.Points[combat.StartTime]))
                        {
                            UpdateSeriesAnnotation(plot.Points[combat.StartTime], plot.Tooltip[combat.StartTime], plot.Name, plot.Abilities[combat.StartTime], false);
                        }
                        if (plot.EffectivePoints.Count > 0 && plot.EffectivePoints.ContainsKey(combat.StartTime) && GraphView.Plot.GetPlottables().Contains(plot.EffectivePoints[combat.StartTime]))
                        {
                            UpdateSeriesAnnotation(plot.EffectivePoints[combat.StartTime], plot.EffectiveTooltip[combat.StartTime], plot.Name + "Effective", plot.Abilities[combat.StartTime], true);
                        }
                    }

                }
                if (previousPointSelected.Any(kvp => kvp.Value != pointSelected[kvp.Key]))
                {
                    foreach (var key in pointSelected.Keys)
                        previousPointSelected[key] = pointSelected[key];
                    Dispatcher.CurrentDispatcher.Invoke(() =>
                    {
                        GraphView.Render();
                    });


                }
            }
        }
        private void PlotCombat(Combat combatToPlot, Entity selectedEntity)
        {

            foreach (var series in _seriesToPlot)
            {
                List<ParsedLogEntry> applicableData = GetCorrectData(series.Type, combatToPlot, selectedEntity);
                if (applicableData == null || applicableData.Count == 0)
                    continue;
                double[] plotXvals;
                double[] plotYvals;
                double[] plotXValRates;
                double[] plotYvaRates;
                if (series.Type != PlotType.HPPercent)
                {
                    plotXvals = PlotMaker.GetPlotXVals(applicableData, combatToPlot.StartTime);
                    plotYvals = PlotMaker.GetPlotYVals(applicableData, false);
                    plotXValRates = PlotMaker.GetPlotXValsRates(plotXvals);
                    plotYvaRates = PlotMaker.GetPlotYValRates(plotYvals, plotXvals);
                }
                else
                {
                    plotXvals = PlotMaker.GetPlotHPXVals(applicableData, combatToPlot.StartTime, selectedEntity);
                    plotYvals = PlotMaker.GetHPPercentages(applicableData, selectedEntity);
                    plotXValRates = plotXvals;
                    plotYvaRates = plotYvals;
                }

                List<(string, string)> abilityNames = PlotMaker.GetAnnotationString(applicableData);
                series.Abilities[combatToPlot.StartTime] = abilityNames;
                var seriesName = _currentCombats.Count == 1 ? series.Name : series.Name + " (" + combatToPlot.StartTime + ")";
                series.Points[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXvals, plotYvals, lineStyle: LineStyle.None, markerShape: GetMarkerFromNumberOfComparisons(_currentCombats.IndexOf(combatToPlot) + 1), label: seriesName, color: series.Color, markerSize: 10);
                if (plotXValRates.Length > 1)
                {
                    series.Line[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXValRates, plotYvaRates, lineStyle: LineStyle.Solid, markerShape: _currentCombats.Count == 1 ? MarkerShape.none : GetMarkerFromNumberOfComparisons(_currentCombats.IndexOf(combatToPlot) + 1), markerSize: 7, label: seriesName + "/s", color: series.Color, lineWidth: 2);
                    series.Line[combatToPlot.StartTime].YAxisIndex = 2;
                }
                if (series.Legend.HasEffective)
                {
                    var effectiveYVals = PlotMaker.GetPlotYVals(applicableData, series.Legend.HasEffective);
                    var effectiveYValSums = PlotMaker.GetPlotYValRates(effectiveYVals, plotXvals);
                    series.EffectivePoints[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXvals, effectiveYVals, lineStyle: LineStyle.None, markerShape: MarkerShape.openCircle, label: "Effective" + seriesName, color: series.Color.Lerp(Color.White, 0.33f), markerSize: 15);
                    series.EffectivePoints[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    if (plotXValRates.Length > 1)
                    {
                        series.EffectiveLine[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXValRates, effectiveYValSums, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, label: "Effective" + seriesName + "/s", color: series.Color.Lerp(Color.White, 0.33f), lineWidth: 2);
                        series.EffectiveLine[combatToPlot.StartTime].YAxisIndex = 2;
                        series.EffectiveLine[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    }

                }

                series.Points[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                if (plotXValRates.Length > 1)
                {
                    series.Line[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                }
                ReInitializeTooltips(series, combatToPlot.StartTime);
            }
            GraphView.Plot.AxisAuto();
            GraphView.Plot.SetAxisLimits(xMin: 0, xMax:(combatToPlot.EndTime - combatToPlot.StartTime).TotalSeconds);
            _combatMetaDataViewModel.PopulateCombatMetaDatas(combatToPlot);
            GraphView.Plot.Render();
        }
        private MarkerShape GetMarkerFromNumberOfComparisons(int numberOfComparison)
        {
            switch (numberOfComparison)
            {
                case 1:
                    return MarkerShape.filledCircle;
                case 2:
                    return MarkerShape.filledDiamond;
                case 3:
                    return MarkerShape.filledSquare;
                default:
                    return MarkerShape.hashTag;
            }
        }
        private void UpdatePlotAxis(object sender, EventArgs e)
        {
            _combatMetaDataViewModel.UpdateBasedOnVisibleData(GraphView.Plot.GetAxisLimits());
        }
        private void UpdateSeriesAnnotation(ScatterPlot plot, Tooltip annotation, string name, List<(string, string)> annotationTexts, bool effective)
        {
            annotation.IsVisible = plot.IsVisible;
            if (!plot.IsVisible)
                return;
            (double mouseCoordX, double mouseCoordY) = GraphView.GetMouseCoordinates();
            double xyRatio = GraphView.Plot.XAxis.Dims.PxPerUnit / GraphView.Plot.YAxis.Dims.PxPerUnit;
            (double pointX, double pointY, int pointIndex) = plot.GetPointNearest(mouseCoordX, mouseCoordY, xyRatio);

            var abilities = annotationTexts;
            if(effective)
                annotation.Label = abilities[pointIndex].Item2;
            else
                annotation.Label = abilities[pointIndex].Item1;

            annotation.X = pointX;
            annotation.Y = pointY;
            

            pointSelected[name] = pointIndex;
            if (!previousPointSelected.ContainsKey(name))
                previousPointSelected[name] = pointIndex;
        }
        private void ResetEffectVisuals()
        {
            lock (graphLock)
            {
                var horizontalSpans = GraphView.Plot.GetPlottables().Where(p => p.GetType() == typeof(HSpan));
                foreach (var span in horizontalSpans)
                {
                    GraphView.Plot.Remove(span);
                }
            }
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
                    return combatToPlot.SheildingProvidedLogs[selectedParticipant];
                case PlotType.HPPercent:
                    return combatToPlot.Logs[selectedParticipant];

            }
            return null;
        }
        private void ReInitializeTooltips(CombatMetaDataSeries series, DateTime startTime)
        {
            
            if (series.Legend.HasEffective)
            {
                series.EffectiveTooltip[startTime] = GraphView.Plot.AddTooltip("test", 0, 0);
                series.EffectiveTooltip[startTime].BorderColor = series.Color;
                series.EffectiveTooltip[startTime].LabelPadding = 2;
                series.EffectiveTooltip[startTime].Font.Size = 15;
                series.EffectiveTooltip[startTime].FillColor = Color.Gray;
                series.EffectiveTooltip[startTime].Font.Color = Color.WhiteSmoke;
                series.EffectiveTooltip[startTime].IsVisible = false;
                series.EffectiveTooltip[startTime].ArrowSize = 10;
            }

            series.Tooltip[startTime] = GraphView.Plot.AddTooltip("test", 0, 0);
            series.Tooltip[startTime].ArrowSize = 10;
            series.Tooltip[startTime].LabelPadding = 2;
            series.Tooltip[startTime].BorderColor = series.Color;
            series.Tooltip[startTime].Font.Size = 15;
            series.Tooltip[startTime].FillColor = Color.Gray;
            series.Tooltip[startTime].Font.Color = Color.WhiteSmoke;
            series.Tooltip[startTime].IsVisible = false;
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
            series.TriggerRender += () =>
            {
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    GraphView.Plot.AxisAuto();
                    GraphView.Render();
                });
            };
            _seriesToPlot.Add(series);
        }

    }
}
