using ScottPlot;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.CombatMetaData;
using SWTORCombatParser.Views.Home_Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using Color = ScottPlot.Color;
using Colors = Avalonia.Media.Colors;

namespace SWTORCombatParser.ViewModels.Home_View_Models
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
    public class PlotViewModel : ReactiveObject
    {
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        private Combat _currentCombat = new Combat();
        private CombatEfffectViewModel _combatMetaDataViewModel;
        private ParticipantSelectionViewModel _participantsViewModel;
        private object graphLock = new object();
        private Entity _selectedParticipant;
        private string averageWindowDuration = "10";
        private double _averageWindowDurationDouble = 10;
        private AvaPlot GraphView;
        private GridLength _secondColumnWidth = _defaultColumnWidth;
        private static GridLength _defaultColumnWidth = new GridLength(.2, GridUnitType.Star);
        private double _userControlWidth;
        private GridLength _participantSelectionHeight;
        private int _minSeletionHeight;
        private int _maxSeletionHeight;
        private ObservableCollection<LegendItemViewModel> _legendItems;

        public double UserControlWidth
        {
            get => _userControlWidth;
            set
            {
                this.RaiseAndSetIfChanged(ref _userControlWidth, value);
                UpdateSecondColumnWidth(value);
            }
        }
       public GridLength SecondColumnWidth
        {
            get => _secondColumnWidth;
            set
            {
                this.RaiseAndSetIfChanged(ref _secondColumnWidth, value);
            }
        }

        public PlotViewModel()
        {
            _combatMetaDataViewModel = new CombatEfffectViewModel();
            _combatMetaDataViewModel.OnEffectSelected += HighlightEffect;
            _combatMetaDataViewModel.OnEffectsCleared += ResetEffectVisuals;
            EffectsView = new EffectsView(_combatMetaDataViewModel);

            ParticipantSelectionContent = new ParticipantSelectionView();
            _participantsViewModel = new ParticipantSelectionViewModel();
            _participantsViewModel.ParticipantSelected += SelectParticipant;
            _participantsViewModel.ViewEnemiesToggled += UpdateParticipantUI;

            ParticipantSelectionContent.DataContext = _participantsViewModel;
        }

        public void SetPlotForViewModel(AvaPlot plot)
        {
            GraphView = plot;
        }
        private Entity SelectedParticipant => _selectedParticipant != null && (_currentCombat != null && !_currentCombat.AllEntities.Any(c => c.Id == _selectedParticipant.Id)) ? _currentCombat.CharacterParticipants.First() : _selectedParticipant;
        private void SelectParticipant(Entity obj)
        {
            if (_selectedParticipant == obj)
                return;

            _selectedParticipant = obj;
            _combatMetaDataViewModel.SelectedParticipant = _selectedParticipant;
            lock (graphLock)
            {
                GraphView.Plot.Clear();
                GraphView.Plot.Axes.AutoScale();
                GraphView.Plot.Axes.SetLimits(left: 0);
                if (_currentCombat == null)
                    return;

                PlotCombat(_currentCombat, obj);

            }
        }
        private void UpdateSecondColumnWidth(double width)
        {
            const double threshold = 500; // Example threshold, adjust as needed
            SecondColumnWidth = width < threshold ? new GridLength(0) : _defaultColumnWidth;
        }
        public ParticipantSelectionView ParticipantSelectionContent { get; set; }

        public GridLength ParticipantSelectionHeight
        {
            get => _participantSelectionHeight;
            set => this.RaiseAndSetIfChanged(ref _participantSelectionHeight, value);
        }

        public int MinSeletionHeight
        {
            get => _minSeletionHeight;
            set
            {
                this.RaiseAndSetIfChanged(ref _minSeletionHeight, value);
                ParticipantSelectionContent.MinHeight = value;
            }
        }

        public int MaxSeletionHeight
        {
            get => _maxSeletionHeight;
            set
            {
                this.RaiseAndSetIfChanged(ref _maxSeletionHeight, value);
                ParticipantSelectionContent.MaxHeight = value;
            }
        }

        public EffectsView EffectsView { get; set; }
        public string AverageWindowDuration
        {
            get => averageWindowDuration;
            set
            {
                double parsedVal = 0;
                if (double.TryParse(value, out parsedVal))
                {
                    if (_averageWindowDurationDouble == parsedVal)
                        return;
                    _averageWindowDurationDouble = parsedVal;
                    GraphView.Plot.Clear();
                    PlotCombat(_currentCombat, _selectedParticipant);
                }
                this.RaiseAndSetIfChanged(ref averageWindowDuration, value);
            }
        }

        public ObservableCollection<LegendItemViewModel> LegendItems
        {
            get => _legendItems;
            set => this.RaiseAndSetIfChanged(ref _legendItems, value);
        }

        public void HighlightEffect(List<CombatModifier> obj)
        {
            lock (graphLock)
            {
                ResetEffectVisuals();
                foreach (var effect in obj)
                {
                    var startTime = _currentCombat.StartTime;
                    var endTime = _currentCombat.EndTime;
                    var maxDuration = (endTime - startTime).TotalSeconds;
                    var effectStart = (effect.StartTime - startTime).TotalSeconds;
                    var effectEnd = (effect.StopTime - startTime).TotalSeconds;
                    GraphView.Plot.Add.HorizontalSpan(Math.Max(effectStart, 0), Math.Min(effectEnd, maxDuration), color: new Color(255,255,197,50 ));
                }
                GraphView.Refresh();
            }
        }

        internal void UpdateParticipants(Combat combat)
        {
            if (_selectedParticipant != null && !combat.AllEntities.Any(e => e.Id == _selectedParticipant.Id))
            {
                if (combat.AllEntities.Any(e => e.LogId == _selectedParticipant.LogId))
                {
                    _selectedParticipant = combat.AllEntities.First(e => e.LogId == _selectedParticipant.LogId);
                }
                else
                    _selectedParticipant = null;
            }

            var setParticipants = _participantsViewModel.UpdateParticipantsData(combat);
            UpdateParticipantUI(setParticipants.Count);
        }

        public void UpdateLivePlot(Combat updatedCombat)
        {

            var updatedParticipants = _participantsViewModel.UpdateParticipantsData(updatedCombat);
            UpdateParticipantUI(updatedParticipants.Count);
            lock (graphLock)
            {
                ResetEffectVisuals();
                Reset();
                PlotCombat(updatedCombat, SelectedParticipant);
            }
        }
        private void UpdateParticipantUI(int viewableEntities)
        {
            ParticipantSelectionHeight = viewableEntities > 8 ? new GridLength(0.2, GridUnitType.Star) : new GridLength(0.1, GridUnitType.Star);
            MinSeletionHeight = viewableEntities > 8 ? 100 : 50;
            MaxSeletionHeight = viewableEntities > 8 ? 150 : 75;
        }
        public void AddCombatPlot(Combat combatToPlot)
        {
            _participantsViewModel.UpdateParticipantsData(combatToPlot);
            lock (graphLock)
            {
                QuietReset();
                ResetEffectVisuals();
                _currentCombat = combatToPlot;
                PlotCombat(combatToPlot, SelectedParticipant);
            }
        }

        public ObservableCollection<LegendItemViewModel> GetLegends()
        {
            return new ObservableCollection<LegendItemViewModel>(_seriesToPlot.Select(s => s.Legend));
        }
        public void QuietReset()
        {
            _currentCombat = null;
            _combatMetaDataViewModel.Reset();
            GraphView.Plot.Clear();
            GraphView.Plot.Axes.AutoScale();
            GraphView.Plot.Axes.SetLimits(left: 0);
        }
        public void Reset()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                _currentCombat = null;
                _combatMetaDataViewModel.Reset();
                GraphView.Plot.Clear();
                GraphView.Plot.Axes.AutoScale();
                GraphView.Plot.Axes.SetLimits(left: 0);
                GraphView.Refresh();
            });
        }
        public void SetSeries(List<CombatMetaDataSeries> series)
        {
            _seriesToPlot = series;
            LegendItems = GetLegends();
        }
        public void MousePositionUpdated(Point position)
        {
            if (_currentCombat == null) return;
            lock (graphLock)
            {
                foreach (var plot in _seriesToPlot)
                {
                    if (plot.EffectiveTooltip != null)
                        plot.EffectiveTooltip.Values.ToList().ForEach(v => v.IsVisible = false);
                    if (plot.Tooltip != null)
                        plot.Tooltip.Values.ToList().ForEach(v => v.IsVisible = false);
                    if (plot.Points.Count > 0 && plot.Points.ContainsKey(_currentCombat.StartTime) && GraphView.Plot.GetPlottables().ToList().Contains(plot.Points[_currentCombat.StartTime]))
                    {
                        UpdateSeriesAnnotation(plot.Points[_currentCombat.StartTime], plot.Tooltip[_currentCombat.StartTime], plot.Name, plot.Abilities[_currentCombat.StartTime], true,position);
                    }
                    if (plot.EffectivePoints.Count > 0 && plot.EffectivePoints.ContainsKey(_currentCombat.StartTime) && GraphView.Plot.GetPlottables().ToList().Contains(plot.EffectivePoints[_currentCombat.StartTime]))
                    {
                        UpdateSeriesAnnotation(plot.EffectivePoints[_currentCombat.StartTime], plot.EffectiveTooltip[_currentCombat.StartTime], plot.Name + "Raw", plot.Abilities[_currentCombat.StartTime], false,position);
                    }

                }
                if (previousPointSelected.Any(kvp => kvp.Value != pointSelected[kvp.Key]))
                {
                    foreach (var key in pointSelected.Keys)
                        previousPointSelected[key] = pointSelected[key];
                }
                GraphView.Refresh();
            }
        }
        private void PlotCombat(Combat combatToPlot, Entity selectedEntity)
        {
            if (selectedEntity == null)
                return;
            foreach (var series in _seriesToPlot)
            {
                List<ParsedLogEntry> applicableData = GetCorrectData(series.Type, combatToPlot, selectedEntity).OrderBy(l => l.TimeStamp).ToList();
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
                }
                else
                {
                    plotXvals = PlotMaker.GetPlotHPXVals(applicableData, combatToPlot.StartTime, selectedEntity);
                    plotYvals = PlotMaker.GetHPPercentages(applicableData, selectedEntity);
                    plotXValRates = plotXvals;
                    plotYvaRates = plotYvals;
                }

                List<(string, string)> abilityNames = PlotMaker.GetAnnotationString(applicableData, series.Type == PlotType.DamageTaken || series.Type == PlotType.HealingTaken, series.Type == PlotType.SheildedDamageTaken);
                series.Abilities[combatToPlot.StartTime] = abilityNames;
                var seriesName = series.Name;
                if (series.Type != PlotType.HPPercent)
                {
                     var normalLine = GraphView.Plot.Add.Scatter(plotXvals, plotYvals,color: series.Color);
                     normalLine.LineStyle = LineStyle.None;
                     normalLine.MarkerShape = GetMarkerFromNumberOfComparisons(1);
                     normalLine.LegendText = seriesName;
                     normalLine.MarkerSize = 5;
                     series.Points[combatToPlot.StartTime] = normalLine;
                     
                    series.Points[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                }
                if (plotXValRates.Length > 1)
                {
                     var rateLine = GraphView.Plot.Add.ScatterLine(plotXValRates, plotYvaRates, color: series.Color);
                     rateLine.MarkerShape = MarkerShape.None;
                     rateLine.MarkerSize = 7;
                     rateLine.LineWidth = 1.5f;
                     series.Line[combatToPlot.StartTime] = rateLine;
                     
                    if (series.Type == PlotType.HPPercent)
                        series.Line[combatToPlot.StartTime].Axes.YAxis = GraphView.Plot.Axes.Right;
                }
                if (series.Legend.HasEffective)
                {
                    var rawYVals = PlotMaker.GetPlotYVals(applicableData, false);
                    var rawYValRates = PlotMaker.GetPlotYValRates(rawYVals, plotXvals, _averageWindowDurationDouble);
                    var effectivePoints = GraphView.Plot.Add.Scatter(plotXvals, rawYVals, color: Color.FromARGB(Avalonia.Media.Color.FromUInt32(series.Color.ARGB).Lerp(Colors.White, 0.33f).ToUInt32()));
                    effectivePoints.LineStyle = LineStyle.None;
                    effectivePoints.MarkerShape = MarkerShape.OpenCircle;
                    effectivePoints.LegendText = "Raw" + seriesName;
                    effectivePoints.MarkerSize = 7;
                    series.EffectivePoints[combatToPlot.StartTime] = effectivePoints;
                    series.EffectivePoints[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    if (plotXValRates.Length > 1)
                    {
                        var rawRate = GraphView.Plot.Add.ScatterLine(plotXValRates, rawYValRates,color: Color.FromARGB(Avalonia.Media.Color.FromUInt32(series.Color.ARGB).Lerp(Colors.White, 0.33f).ToUInt32()));
                        rawRate.MarkerShape = MarkerShape.None;
                        rawRate.MarkerSize = 7;
                        rawRate.LineWidth = 1.5f;
                        series.EffectiveLine[combatToPlot.StartTime] = rawRate;
                        series.EffectiveLine[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    }
                }


                if (plotXValRates.Length > 1)
                {
                    series.Line[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                }
                ReInitializeTooltips(series, combatToPlot.StartTime);
            }
            try
            {
                GraphView.Plot.Axes.AutoScale();
            }
            catch (InvalidOperationException ex)
            {
                GraphView.Plot.Axes.SetLimits(bottom: 0, top: 0);
            }
            GraphView.Plot.Axes.SetLimits(bottom: 0);
            //need to be sure that xmax is greater that xmin
            GraphView.Plot.Axes.SetLimits(left: 0, right: Math.Max(1, (combatToPlot.EndTime - combatToPlot.StartTime).TotalSeconds));
            _combatMetaDataViewModel.PopulateEffectsFromCombat(combatToPlot);
            Dispatcher.UIThread.Invoke(() =>
            {
                GraphView.Refresh();
            });
        }


        private MarkerShape GetMarkerFromNumberOfComparisons(int numberOfComparison)
        {
            switch (numberOfComparison)
            {
                case 1:
                    return MarkerShape.FilledCircle;
                case 2:
                    return MarkerShape.FilledDiamond;
                case 3:
                    return MarkerShape.FilledSquare;
                default:
                    return MarkerShape.HashTag;
            }
        }
        public void UpdatePlotAxis(AxisLimits limits)
        {
            if (CombatLogStreamer.InCombat)
                return;
            _combatMetaDataViewModel.UpdateBasedOnVisibleData(limits);
        }
        private void UpdateSeriesAnnotation(Scatter plot, Callout annotation, string name, List<(string, string)> annotationTexts, bool effective, Point mousePos)
        {
            if (!plot.IsVisible)
                return;
            var coords = GraphView.Plot.GetCoordinates((float)mousePos.X, (float)mousePos.Y);
            var point = plot.Data.GetNearest(coords, GraphView.Plot.LastRender,30);
            if(point.Index == -1)
                return;
            annotation.IsVisible = plot.IsVisible;
            var abilities = annotationTexts;
            if (effective)
                annotation.Text = abilities[point.Index].Item2;
            else
                annotation.Text = abilities[point.Index].Item1;

            annotation.TipCoordinates = new Coordinates(point.X,point.Y);
            annotation.TextCoordinates = GraphView.Plot.GetCoordinates((float)mousePos.X+5, (float)mousePos.Y);


            pointSelected[name] = point.Index;
            if (!previousPointSelected.ContainsKey(name))
                previousPointSelected[name] = point.Index;
        }
        private void ResetEffectVisuals()
        {
            lock (graphLock)
            {
                var horizontalSpans = GraphView.Plot.GetPlottables().Where(p => p.GetType() == typeof(HorizontalSpan)).ToList();
                foreach (var span in horizontalSpans)
                {
                    GraphView.Plot.Remove(span);
                }

            }
            Dispatcher.UIThread.Invoke(() =>
            {
                GraphView.Refresh();
            });
        }
        private ConcurrentQueue<ParsedLogEntry> GetCorrectData(PlotType type, Combat combatToPlot, Entity selectedParticipant)
        {
            if (combatToPlot == null)
                return new ConcurrentQueue<ParsedLogEntry>();
            switch (type)
            {
                case PlotType.DamageOutput:
                    return combatToPlot.OutgoingDamageLogs.ContainsKey(selectedParticipant) ? combatToPlot.OutgoingDamageLogs[selectedParticipant] : new ConcurrentQueue<ParsedLogEntry>();
                case PlotType.DamageTaken:
                    return combatToPlot.IncomingDamageLogs.ContainsKey(selectedParticipant) ? combatToPlot.IncomingDamageLogs[selectedParticipant] : new ConcurrentQueue<ParsedLogEntry>();
                case PlotType.HealingOutput:
                    return combatToPlot.OutgoingHealingLogs.ContainsKey(selectedParticipant) ? combatToPlot.OutgoingHealingLogs[selectedParticipant] : new ConcurrentQueue<ParsedLogEntry>();
                case PlotType.HealingTaken:
                    return combatToPlot.IncomingHealingLogs.ContainsKey(selectedParticipant) ? combatToPlot.IncomingHealingLogs[selectedParticipant] : new ConcurrentQueue<ParsedLogEntry>();
                case PlotType.SheildedDamageTaken:
                    return combatToPlot.ShieldingProvidedLogs.ContainsKey(selectedParticipant) ? combatToPlot.ShieldingProvidedLogs[selectedParticipant] : new ConcurrentQueue<ParsedLogEntry>();
                case PlotType.HPPercent:
                    return combatToPlot.GetLogsInvolvingEntity(selectedParticipant);

            }
            return null;
        }
        private void ReInitializeTooltips(CombatMetaDataSeries series, DateTime startTime)
        {
            var backgroundcolor = ResourceFinder.GetColorFromResourceName("Gray4");
            if (series.Legend.HasEffective)
            {
                series.EffectiveTooltip[startTime] = GraphView.Plot.Add.Callout("test", new Coordinates(0,0), new Coordinates(0,0));
                series.EffectiveTooltip[startTime].LabelBorderColor = series.Color;
                series.EffectiveTooltip[startTime].ArrowFillColor = series.Color;
                series.EffectiveTooltip[startTime].LabelPadding = 1;
                series.EffectiveTooltip[startTime].FontSize = 11;
                series.EffectiveTooltip[startTime].TextBackgroundColor = new Color(backgroundcolor.R, backgroundcolor.G, backgroundcolor.B,backgroundcolor.A);
                series.EffectiveTooltip[startTime].TextColor = Color.FromARGB(Colors.WhiteSmoke.ToUInt32());
                series.EffectiveTooltip[startTime].IsVisible = false;
                series.EffectiveTooltip[startTime].ArrowWidth = 2;
                series.EffectiveTooltip[startTime].ArrowheadWidth = 5;
                series.EffectiveTooltip[startTime].ArrowheadLength = 3;
            }

            series.Tooltip[startTime] = GraphView.Plot.Add.Callout("test", new Coordinates(0,0), new Coordinates(0,0));
            series.Tooltip[startTime].ArrowWidth = 2;
            series.Tooltip[startTime].ArrowheadWidth = 5;
            series.Tooltip[startTime].ArrowheadLength = 3;
            series.Tooltip[startTime].LabelPadding = 1;
            series.Tooltip[startTime].LabelBorderColor = series.Color;
            series.Tooltip[startTime].ArrowFillColor = series.Color;
            series.Tooltip[startTime].FontSize = 11;
            series.Tooltip[startTime].TextBackgroundColor = new Color( backgroundcolor.R, backgroundcolor.G, backgroundcolor.B,backgroundcolor.A);
            series.Tooltip[startTime].TextColor = Color.FromARGB(Colors.WhiteSmoke.ToUInt32());
            series.Tooltip[startTime].IsVisible = false;
        }
    }
}
