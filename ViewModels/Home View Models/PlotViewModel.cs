using ScottPlot;
using ScottPlot.Plottable;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Plotting;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.CombatMetaData;
using SWTORCombatParser.Views.Home_Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

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
    public class PlotViewModel : INotifyPropertyChanged
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
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public PlotViewModel()
        {
            _combatMetaDataViewModel = new CombatEfffectViewModel();
            _combatMetaDataViewModel.OnEffectSelected += HighlightEffect;
            _combatMetaDataViewModel.OnEffectsCleared += ResetEffectVisuals;
            CombatMetaDataView = new CombatMetaDataView(_combatMetaDataViewModel);

            ParticipantSelectionContent = new ParticipantSelectionView();
            _participantsViewModel = new ParticipantSelectionViewModel();
            _participantsViewModel.ParticipantSelected += SelectParticipant;
            _participantsViewModel.ViewEnemiesToggled += UpdateParticipantUI;

            ParticipantSelectionContent.DataContext = _participantsViewModel;
            GraphView = new WpfPlot();
            GraphView.Plot.XAxis.Label(label: "Combat Duration (s)", size: 12);
            GraphView.Plot.YAxis.Label(label: "Value", size: 12);

            GraphView.AxesChanged += UpdatePlotAxis;
            var legend = GraphView.Plot.Legend(location: Alignment.UpperRight);
            legend.FillColor = Color.FromArgb(50, 50, 50, 50);
            legend.FontColor = Color.WhiteSmoke;
            legend.FontSize = 10;
            ConfigureSeries(Enum.GetValues(typeof(PlotType)).Cast<PlotType>().ToList());
            LegendItems = GetLegends();
            GraphView.Plot.Style(dataBackground: Color.FromArgb(100, 10, 10, 10),
                figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 120, 120, 120), tick: Color.LightGray, axisLabel: Color.WhiteSmoke, titleLabel: Color.WhiteSmoke);
            GraphView.Plot.AddPoint(0, 0, color: Color.Transparent);
            GraphView.Refresh();
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
                GraphView.Plot.AxisAuto();
                GraphView.Plot.SetAxisLimits(xMin: 0);
                if (_currentCombat == null)
                    return;

                PlotCombat(_currentCombat, obj);

            }
        }
        public ParticipantSelectionView ParticipantSelectionContent { get; set; }
        public GridLength ParticipantSelectionHeight { get; set; }
        public int MinSeletionHeight { get; set; }
        public int MaxSeletionHeight { get; set; }
        public CombatMetaDataView CombatMetaDataView { get; set; }
        public WpfPlot GraphView { get; set; }
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
                        AddSeries(plotType, "DPS", Color.LightCoral, true);
                        break;
                    case PlotType.DamageTaken:
                        AddSeries(plotType, "DTPS", Color.Peru, true);
                        break;
                    case PlotType.SheildedDamageTaken:
                        AddSeries(plotType, "Absorb", Color.WhiteSmoke);
                        break;
                    case PlotType.HealingOutput:
                        AddSeries(plotType, "HPS", Color.MediumAquamarine, true);
                        break;
                    case PlotType.HealingTaken:
                        AddSeries(plotType, "HRPS", Color.LightSkyBlue, true);
                        break;
                    case PlotType.HPPercent:
                        AddSeries(plotType, "HP", Color.LightGoldenrodYellow, false, false);
                        break;
                    default:
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
                    var startTime = _currentCombat.StartTime;
                    var endTime = _currentCombat.EndTime;
                    var maxDuration = (endTime - startTime).TotalSeconds;
                    var effectStart = (effect.StartTime - startTime).TotalSeconds;
                    var effectEnd = (effect.StopTime - startTime).TotalSeconds;
                    GraphView.Plot.AddHorizontalSpan(Math.Max(effectStart, 0), Math.Min(effectEnd, maxDuration), color: Color.FromArgb(50, Color.LightYellow));
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

            OnPropertyChanged("ParticipantSelectionHeight");
            OnPropertyChanged("MinSeletionHeight");
            OnPropertyChanged("MaxSeletionHeight");
        }
        public void AddCombatPlot(Combat combatToPlot)
        {
            _participantsViewModel.UpdateParticipantsData(combatToPlot);
            lock (graphLock)
            {
                QuietReset();
                ResetEffectVisuals();
                _currentCombat=combatToPlot;
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
            GraphView.Plot.AxisAuto();
            GraphView.Plot.SetAxisLimits(xMin: 0);
        }
        public void Reset()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                _currentCombat = null;
                _combatMetaDataViewModel.Reset();
                GraphView.Plot.Clear();
                GraphView.Plot.AxisAuto();
                GraphView.Plot.SetAxisLimits(xMin: 0);
                GraphView.Refresh();
            });
        }
        public void MousePositionUpdated()
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
                    if (plot.Points.Count > 0 && plot.Points.ContainsKey(_currentCombat.StartTime) && GraphView.Plot.GetPlottables().Contains(plot.Points[_currentCombat.StartTime]))
                    {
                        UpdateSeriesAnnotation(plot.Points[_currentCombat.StartTime], plot.Tooltip[_currentCombat.StartTime], plot.Name, plot.Abilities[_currentCombat.StartTime], true);
                    }
                    if (plot.EffectivePoints.Count > 0 && plot.EffectivePoints.ContainsKey(_currentCombat.StartTime) && GraphView.Plot.GetPlottables().Contains(plot.EffectivePoints[_currentCombat.StartTime]))
                    {
                        UpdateSeriesAnnotation(plot.EffectivePoints[_currentCombat.StartTime], plot.EffectiveTooltip[_currentCombat.StartTime], plot.Name + "Raw", plot.Abilities[_currentCombat.StartTime], false);
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
            if(selectedEntity == null)
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
                    //PlotPeaks(series, plotYvaRates,plotXValRates, combatToPlot, selectedEntity);
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
                    series.Points[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXvals, plotYvals, lineStyle: LineStyle.None, markerShape: GetMarkerFromNumberOfComparisons(1), label: seriesName, color: series.Color, markerSize: 5);
                    series.Points[combatToPlot.StartTime].IsVisible = series.Legend.Checked;
                }
                if (plotXValRates.Length > 1)
                {
                    series.Line[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXValRates, plotYvaRates, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, markerSize: 7, color: series.Color, lineWidth: 1.5f);
                    if (series.Type == PlotType.HPPercent)
                        series.Line[combatToPlot.StartTime].YAxisIndex = 1;
                }
                if (series.Legend.HasEffective)
                {
                    var rawYVals = PlotMaker.GetPlotYVals(applicableData, false);
                    var rawYValRates = PlotMaker.GetPlotYValRates(rawYVals, plotXvals, _averageWindowDurationDouble);
                    series.EffectivePoints[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXvals, rawYVals, lineStyle: LineStyle.None, markerShape: MarkerShape.openCircle, label: "Raw" + seriesName, color: series.Color.Lerp(Color.White, 0.33f), markerSize: 7);
                    series.EffectivePoints[combatToPlot.StartTime].IsVisible = series.Legend.EffectiveChecked;
                    if (plotXValRates.Length > 1)
                    {
                        series.EffectiveLine[combatToPlot.StartTime] = GraphView.Plot.AddScatter(plotXValRates, rawYValRates, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, color: series.Color.Lerp(Color.White, 0.33f), lineWidth: 1.5f);
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
                GraphView.Plot.AxisAuto();
            }
            catch (InvalidOperationException ex)
            {
                GraphView.Plot.SetAxisLimits(yMin: 0, yMax: 0, yAxisIndex: 0);
            }
            GraphView.Plot.SetAxisLimits(yMin: 0, yAxisIndex: 1);
            //need to be sure that xmax is greater that xmin
            GraphView.Plot.SetAxisLimits(xMin: 0, xMax: Math.Max(1, (combatToPlot.EndTime - combatToPlot.StartTime).TotalSeconds));
            _combatMetaDataViewModel.PopulateEffectsFromCombat(combatToPlot);
            App.Current.Dispatcher.Invoke(() => {
                GraphView.Refresh();
            });     
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
            if (CombatDetector.InCombat)
                return;
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
                
            }
            App.Current.Dispatcher.Invoke(() => {
                GraphView.Refresh();
            });
        }
        private List<ParsedLogEntry> GetCorrectData(PlotType type, Combat combatToPlot, Entity selectedParticipant)
        {
            switch (type)
            {
                case PlotType.DamageOutput:
                    return combatToPlot.OutgoingDamageLogs.ContainsKey(selectedParticipant) ? combatToPlot.OutgoingDamageLogs[selectedParticipant] : new List<ParsedLogEntry>();
                case PlotType.DamageTaken:
                    return combatToPlot.IncomingDamageLogs.ContainsKey(selectedParticipant) ? combatToPlot.IncomingDamageLogs[selectedParticipant] : new List<ParsedLogEntry>();
                case PlotType.HealingOutput:
                    return combatToPlot.OutgoingHealingLogs.ContainsKey(selectedParticipant) ? combatToPlot.OutgoingHealingLogs[selectedParticipant] : new List<ParsedLogEntry>();
                case PlotType.HealingTaken:
                    return combatToPlot.IncomingHealingLogs.ContainsKey(selectedParticipant) ? combatToPlot.IncomingHealingLogs[selectedParticipant] : new List<ParsedLogEntry>();
                case PlotType.SheildedDamageTaken:
                    return combatToPlot.ShieldingProvidedLogs.ContainsKey(selectedParticipant) ? combatToPlot.ShieldingProvidedLogs[selectedParticipant] : new List<ParsedLogEntry>();
                case PlotType.HPPercent:
                    return combatToPlot.GetLogsInvolvingEntity(selectedParticipant).ToList();

            }
            return null;
        }
        private void ReInitializeTooltips(CombatMetaDataSeries series, DateTime startTime)
        {
            var backgroundcolor = ResourceFinder.GetColorFromResourceName("Gray4");
            if (series.Legend.HasEffective)
            {
                series.EffectiveTooltip[startTime] = GraphView.Plot.AddTooltip("test", 0, 0);
                series.EffectiveTooltip[startTime].BorderColor = series.Color;
                series.EffectiveTooltip[startTime].LabelPadding = 1;
                series.EffectiveTooltip[startTime].Font.Size = 11;
                series.EffectiveTooltip[startTime].FillColor = Color.FromArgb(backgroundcolor.A, backgroundcolor.R, backgroundcolor.G, backgroundcolor.B);
                series.EffectiveTooltip[startTime].Font.Color = Color.WhiteSmoke;
                series.EffectiveTooltip[startTime].IsVisible = false;
                series.EffectiveTooltip[startTime].ArrowSize = 5;
            }

            series.Tooltip[startTime] = GraphView.Plot.AddTooltip("test", 0, 0);
            series.Tooltip[startTime].ArrowSize = 5;
            series.Tooltip[startTime].LabelPadding = 1;
            series.Tooltip[startTime].BorderColor = series.Color;
            series.Tooltip[startTime].Font.Size = 11;
            series.Tooltip[startTime].FillColor = Color.FromArgb(backgroundcolor.A, backgroundcolor.R, backgroundcolor.G, backgroundcolor.B);
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
                    GraphView.Plot.YAxis2.Label("Health", size: 12);
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
