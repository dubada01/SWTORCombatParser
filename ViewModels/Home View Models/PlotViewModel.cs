using ScottPlot;
using ScottPlot.Plottable;
using ScottPlot.Renderable;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels;
using SWTORCombatParser.ViewModels.Home_View_Models;
using SWTORCombatParser.Views.Home_Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows;
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
    public class PlotViewModel : INotifyPropertyChanged
    {
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        private List<Combat> _currentCombats = new List<Combat>();
        private CombatMetaDataViewModel _combatMetaDataViewModel;
        private ParticipantSelectionViewModel _participantsViewModel;
        private object graphLock = new object();
        private Entity _currentParticipant;
        private string averageWindowDuration = "10";
        private double _averageWindowDurationDouble = 10;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public PlotViewModel()
        {
            _combatMetaDataViewModel = new CombatMetaDataViewModel();
            _combatMetaDataViewModel.OnEffectSelected += HighlightEffect;
            _combatMetaDataViewModel.OnEffectsCleared += ResetEffectVisuals;
            CombatMetaDataView = new CombatMetaDataView(_combatMetaDataViewModel);

            ParticipantSelectionContent = new ParticipantSelectionView();
            _participantsViewModel = new ParticipantSelectionViewModel();
            _participantsViewModel.ParticipantSelected += SelectParticipant;

            ParticipantSelectionContent.DataContext = _participantsViewModel;
            GraphView = new WpfPlot();
            GraphView.Plot.XLabel("Combat Duration (s)");
            GraphView.Plot.YLabel("Value");
            GraphView.AxesChanged += UpdatePlotAxis;
            var legend = GraphView.Plot.Legend(location: Alignment.UpperRight);
            legend.FillColor = Color.FromArgb(50, 50, 50, 50);
            legend.FontColor = Color.WhiteSmoke;
            legend.FontSize = 15;
            ConfigureSeries(Enum.GetValues(typeof(PlotType)).Cast<PlotType>().ToList());
            LegendItems = GetLegends();
            GraphView.Plot.Style(dataBackground: Color.FromArgb(150, 10, 10, 10), figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 40, 40, 40));
            GraphView.Plot.AddPoint(0, 0, color: Color.Transparent);
            GraphView.Refresh();
        }
        private Entity SelectedParticipant => _currentParticipant == null || !_currentCombats.First().CharacterParticipants.Contains(_currentParticipant) ? _currentCombats.First().CharacterParticipants.First() : _currentParticipant;
        private void SelectParticipant(Entity obj)
        {
            if (_currentParticipant == obj)
                return;

            _currentParticipant = obj;
            _combatMetaDataViewModel.SelectedParticipant = _currentParticipant;
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
        public ParticipantSelectionView ParticipantSelectionContent { get; set; }
        public GridLength ParticipantSelectionHeight { get; set; }
        public CombatMetaDataView CombatMetaDataView { get; set; }
        public WpfPlot GraphView { get; set; }
        public string AverageWindowDuration { get => averageWindowDuration; 
            set {
                double parsedVal = 0;
                if(double.TryParse(value,out parsedVal))
                {
                    if (_averageWindowDurationDouble == parsedVal)
                        return;
                    _averageWindowDurationDouble = parsedVal;
                    GraphView.Plot.Clear();
                    foreach (var combat in _currentCombats)
                    {
                        PlotCombat(combat, _currentParticipant);
                    }
                    OnPropertyChanged("AverageWindowDuration");
                }
                averageWindowDuration = value;
            }
        }
        public ObservableCollection<LegendItemViewModel> LegendItems { get; set; }
        public void ConfigureSeries(List<PlotType> seriesToPlot)
        {
            foreach (var plotType in seriesToPlot)
            {
                switch (plotType)
                {
                    case PlotType.DamageOutput:
                        AddSeries(plotType, "Damage Output", Color.LightCoral, true);
                        break;
                    case PlotType.DamageTaken:
                        AddSeries(plotType, "Damage Incoming", Color.Peru, true);
                        break;
                    case PlotType.SheildedDamageTaken:
                        AddSeries(plotType, "Sheilding", Color.WhiteSmoke);
                        break;
                    case PlotType.HealingOutput:
                        AddSeries(plotType, "Heal Output", Color.MediumAquamarine, true);
                        break;
                    case PlotType.HealingTaken:
                        AddSeries(plotType, "Heal Incoming", Color.LightSkyBlue, true);
                        break;
                    case PlotType.HPPercent:
                        AddSeries(plotType, "Health Percentage", Color.LightGoldenrodYellow, false, false);
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
                GraphView.Refresh();
            }
        }

        internal void UpdateParticipants(List<Entity> obj)
        {
            ParticipantSelectionHeight = obj.Count > 4 ? new GridLength(0.25, GridUnitType.Star) : new GridLength(0.125, GridUnitType.Star);
            OnPropertyChanged("ParticipantSelectionHeight");
            _combatMetaDataViewModel.AvailableParticipants = obj;
            _participantsViewModel.SetParticipants(obj);
            if (_currentParticipant == null)
                _participantsViewModel.SelectLocalPlayer();
            else
                _participantsViewModel.SelectParticipant(_currentParticipant);
        }

        public void UpdateLivePlot(Combat updatedCombat)
        {
            _participantsViewModel.UpdateParticipantsData(updatedCombat);
            lock (graphLock)
            {
                ResetEffectVisuals();
                var staleCombat = _currentCombats.FirstOrDefault(c => c.StartTime == updatedCombat.StartTime);
                if (staleCombat != null)
                {
                    RemoveCombatPlot(staleCombat);
                }

                _currentCombats.Add(updatedCombat);
                PlotCombat(updatedCombat, SelectedParticipant);
            }
        }
        public void AddCombatPlot(Combat combatToPlot)
        {
            _participantsViewModel.UpdateParticipantsData(combatToPlot);
            lock (graphLock)
            {
                ResetEffectVisuals();
                _currentCombats.Add(combatToPlot);
                PlotCombat(combatToPlot, SelectedParticipant);
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
                    PlotCombat(remainingCombat, SelectedParticipant);
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
                GraphView.Refresh();
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
                            UpdateSeriesAnnotation(plot.Points[combat.StartTime], plot.Tooltip[combat.StartTime], plot.Name, plot.Abilities[combat.StartTime], true);
                        }
                        if (plot.EffectivePoints.Count > 0 && plot.EffectivePoints.ContainsKey(combat.StartTime) && GraphView.Plot.GetPlottables().Contains(plot.EffectivePoints[combat.StartTime]))
                        {
                            UpdateSeriesAnnotation(plot.EffectivePoints[combat.StartTime], plot.EffectiveTooltip[combat.StartTime], plot.Name + "Raw", plot.Abilities[combat.StartTime], false);
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
                List<ParsedLogEntry> applicableData = GetCorrectData(series.Type, combatToPlot, selectedEntity).OrderBy(l=>l.TimeStamp).ToList();
                if (applicableData == null || applicableData.Count == 0)
                    continue;
                double[] plotXvals;
                double[] plotYvals;
                double[] plotXValRates;
                double[] plotYvaRates;
                if (series.Type != PlotType.HPPercent)
                {
                    plotXvals = PlotMaker.GetPlotXVals(applicableData, combatToPlot.StartTime);
                    plotYvals = PlotMaker.GetPlotYVals(applicableData, true);
                    plotXValRates = PlotMaker.GetPlotXValsRates(plotXvals);
                    plotYvaRates = PlotMaker.GetPlotYValRates(plotYvals, plotXvals, _averageWindowDurationDouble);
                    //PlotPeaks(series, plotYvaRates,plotXValRates, combatToPlot, selectedEntity);
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
                if (series.Type != PlotType.HPPercent)
                {
                    series.Points[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXvals, plotYvals, lineStyle: LineStyle.None, markerShape: GetMarkerFromNumberOfComparisons(_currentCombats.IndexOf(combatToPlot) + 1), label: seriesName, color: series.Color, markerSize: 10);
                    series.Points[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                }
                if (plotXValRates.Length > 1)
                {
                    series.Line[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXValRates, plotYvaRates, lineStyle: LineStyle.Solid, markerShape: _currentCombats.Count == 1 ? MarkerShape.none : GetMarkerFromNumberOfComparisons(_currentCombats.IndexOf(combatToPlot) + 1), markerSize: 7, label: seriesName + "/s", color: series.Color, lineWidth: 2);
                    if (series.Type == PlotType.HPPercent)
                        series.Line[combatToPlot.StartTime].YAxisIndex = 1;
                }
                if (series.Legend.HasEffective)
                {
                    var rawYVals = PlotMaker.GetPlotYVals(applicableData, false);
                    var rawYValRates = PlotMaker.GetPlotYValRates(rawYVals, plotXvals,_averageWindowDurationDouble);
                    series.EffectivePoints[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXvals, rawYVals, lineStyle: LineStyle.None, markerShape: MarkerShape.openCircle, label: "Raw" + seriesName, color: series.Color.Lerp(Color.White, 0.33f), markerSize: 15);
                    series.EffectivePoints[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    if (plotXValRates.Length > 1)
                    {
                        series.EffectiveLine[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXValRates, rawYValRates, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, label: "Raw" + seriesName + "/s", color: series.Color.Lerp(Color.White, 0.33f), lineWidth: 2);
                        series.EffectiveLine[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    }

                }

                
                if (plotXValRates.Length > 1)
                {
                    series.Line[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                }
                ReInitializeTooltips(series, combatToPlot.StartTime);
            }
            GraphView.Plot.AxisAuto();
            GraphView.Plot.SetAxisLimits(yMin: 0, yAxisIndex: 1);
            GraphView.Plot.SetAxisLimits(xMin: 0, xMax: (combatToPlot.EndTime - combatToPlot.StartTime).TotalSeconds);
            _combatMetaDataViewModel.PopulateCombatMetaDatas(combatToPlot);
            GraphView.Refresh();
        }

        private void PlotPeaks(CombatMetaDataSeries series, double[] plotYvaRates, double[] rateTimeStamps, Combat combatToPlot, Entity selectedEntity)
        {
            List<(int, double)> peaksAndIndicies = PlotMaker.GetPeaksOfMean(plotYvaRates, _averageWindowDurationDouble);
            var peaksXVals = new List<double>();
            var peaks = new List<double>();
            for (var i = 0; i < peaksAndIndicies.Count; i++)
            {
                var peak = peaksAndIndicies[i];
                if (peak.Item2 == 0)
                    continue;
                peaksXVals.Add(rateTimeStamps[peak.Item1]);
                peaks.Add(peak.Item2);
            }
            if (peaksXVals.Count == 0)
                return;
            GraphView.Plot.AddScatter(peaksXVals.ToArray(), peaks.ToArray(), lineStyle: LineStyle.None, markerShape: MarkerShape.asterisk, color: series.Color, markerSize: 5);
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
            if (effective)
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
                GraphView.Refresh();
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
                    return combatToPlot.GetLogsInvolvingEntity(selectedParticipant);

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
            series.TriggerRender += (toggleState) =>
            {
                if (toggleState && type == PlotType.HPPercent)
                {
                    GraphView.Plot.YAxis2.Label("Health");
                    GraphView.Plot.YAxis2.Ticks(true);
                }
                if (!toggleState && type == PlotType.HPPercent)
                {
                    GraphView.Plot.YAxis2.Label("");
                    GraphView.Plot.YAxis2.Ticks(false);
                }
                Dispatcher.CurrentDispatcher.Invoke(() =>
                {
                    GraphView.Plot.AxisAuto();
                    GraphView.Plot.SetAxisLimits(yMin: 0, yAxisIndex: 1);
                    GraphView.Refresh();
                });
            };
            _seriesToPlot.Add(series);
        }

    }
}
