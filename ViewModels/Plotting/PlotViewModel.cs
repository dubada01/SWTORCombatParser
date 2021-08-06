using ScottPlot;
using ScottPlot.Plottable;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Threading;

namespace SWTORCombatParser.Plotting
{
    public enum PlotType
    {
        DamageOutput,
        DamageTaken,
        HealingOutput,
        HealingTaken,
        SheildedDamageTaken
    }
    public class PlotViewModel
    {
        private WpfPlot _plotToManage;
        
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        public PlotViewModel()
        {
            _plotToManage = new WpfPlot();
            _plotToManage.Plot.XLabel("Combat Duration (s)");
            _plotToManage.Plot.YLabel("Ammount");

            _plotToManage.Plot.AddAxis(ScottPlot.Renderable.Edge.Right, 2, title: "Rate");

            var legend = _plotToManage.Plot.Legend(location: Alignment.UpperRight);
            legend.FillColor = Color.FromArgb(50, 50, 50, 50);
            legend.FontColor = Color.WhiteSmoke;
            legend.FontSize = 15;
            SetUpLegend(new List<PlotType> { PlotType.DamageOutput, PlotType.DamageTaken, PlotType.HealingOutput, PlotType.HealingTaken });
            LegendItems = GetLegends();
            _plotToManage.Plot.Style(dataBackground: Color.FromArgb(100, 20, 20, 20), figureBackground: Color.FromArgb(0,10, 10, 10),grid: Color.FromArgb(100, 40, 40, 40));
        }
        public WpfPlot GraphView => _plotToManage;
        public ObservableCollection<LegendItemViewModel> LegendItems { get; set; }
        public void SetUpLegend(List<PlotType> seriesToPlot)
        {
            foreach (var plotType in seriesToPlot)
            {
                switch (plotType)
                {
                    case PlotType.DamageOutput:
                        AddSeries(plotType,"Damage Output",Color.MediumVioletRed);
                        break;
                    case PlotType.DamageTaken:
                        AddSeries(plotType, "Damage Incoming",Color.Peru,true);
                        break;
                    case PlotType.SheildedDamageTaken:
                        AddSeries(plotType, "Sheilded Damage Incoming", Color.Magenta);
                        break;
                    case PlotType.HealingOutput:
                        AddSeries(plotType, "Heal Output", Color.LimeGreen,true);
                        break;
                    case PlotType.HealingTaken:
                        AddSeries(plotType, "Heal Incoming", Color.CornflowerBlue,true);
                        break;
                    default:
                        Trace.WriteLine("Invalid Series");
                        break;
                }
            }
        }
        public void PlotData(Combat combatToPlot)
        {
            foreach(var series in _seriesToPlot)
            {
                _plotToManage.Plot.Remove(series.Points);
                _plotToManage.Plot.Remove(series.Line);
                _plotToManage.Plot.Remove(series.EffectivePoints);
                _plotToManage.Plot.Remove(series.EffectiveLine);
            }
            
            foreach (var series in _seriesToPlot)
            {
                List<ParsedLogEntry> applicableData = GetCorrectData(series.Type, combatToPlot);
                if (applicableData.Count == 0)
                    continue;
                var plotXvals = PlotMaker.GetPlotXVals(applicableData, combatToPlot.StartTime);
                var plotYvals = PlotMaker.GetPlotYVals(applicableData,false);
                var plotYvalSums = PlotMaker.GetPlotYValRates(applicableData, plotXvals,false);
                List<string> abilityNames = PlotMaker.GetAnnotationString(applicableData);
                series.Abilities = abilityNames;
                series.Points = _plotToManage.Plot.AddScatter(plotXvals, plotYvals, lineStyle: LineStyle.None, markerShape: MarkerShape.filledCircle, label: series.Name, color: series.Color, markerSize: 10);
                series.Line = _plotToManage.Plot.AddScatter(plotXvals, plotYvalSums, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, label: series.Name + "/s", color: series.Color, lineWidth:2);
                series.Line.YAxisIndex = 2;
                if (series.Legend.HasEffective)
                {
                    var effectiveYVals = PlotMaker.GetPlotYVals(applicableData, series.Legend.HasEffective);
                    var effectiveYValSums = PlotMaker.GetPlotYValRates(applicableData, plotXvals, series.Legend.HasEffective);
                    
                    series.EffectivePoints = _plotToManage.Plot.AddScatter(plotXvals, effectiveYVals, lineStyle: LineStyle.None, markerShape: MarkerShape.openCircle, label: "Effective" + series.Name, color: series.Color.Lerp(Color.White,0.33f), markerSize: 15);
                    series.EffectiveLine = _plotToManage.Plot.AddScatter(plotXvals, effectiveYValSums, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, label: "Effective" + series.Name + "/s", color: series.Color.Lerp(Color.White, 0.33f), lineWidth: 2);
                    series.EffectiveLine.YAxisIndex = 2;
                    series.EffectivePoints.IsVisible = series.Legend.EffectiveChecked;
                    series.EffectiveLine.IsVisible = series.Legend.EffectiveChecked;
                }

                series.Points.IsVisible = series.Legend.Checked;
                series.Line.IsVisible = series.Legend.Checked;
            }
            _plotToManage.Plot.AxisAuto();
        }
        public ObservableCollection<LegendItemViewModel> GetLegends()
        {
            return new ObservableCollection<LegendItemViewModel>(_seriesToPlot.Select(s => s.Legend));
        }
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
        public void MousePositionUpdated()
        {
            foreach(var plot in _seriesToPlot)
            {
                if (plot.Points != null)
                {
                    UpdateSeriesAnnotation(plot.Points, plot.Annotation, plot.Name, plot.Abilities); 
                }
                if (plot.EffectivePoints != null)
                {
                    UpdateSeriesAnnotation(plot.EffectivePoints, plot.EffectiveAnnotation, plot.Name + "Effective", plot.Abilities); 
                }
            }
            if (previousPointSelected.Any(kvp => kvp.Value != pointSelected[kvp.Key]))
            {
                foreach (var key in pointSelected.Keys)
                    previousPointSelected[key] = pointSelected[key];
                Dispatcher.CurrentDispatcher.Invoke(() => {
                    _plotToManage.Render();
                });
            }
        }

        private void UpdateSeriesAnnotation(ScatterPlot plot, Annotation annotation, string name, List<string> annotationTexts)
        {
            annotation.IsVisible = plot.IsVisible;
            (double mouseCoordX, double mouseCoordY) = _plotToManage.GetMouseCoordinates();
            double xyRatio = _plotToManage.Plot.XAxis.Dims.PxPerUnit / _plotToManage.Plot.YAxis.Dims.PxPerUnit;
            (double pointX, double pointY, int pointIndex) = plot.GetPointNearest(mouseCoordX, mouseCoordY, xyRatio);

            var abilities = annotationTexts;
            annotation.X = (pointX * _plotToManage.Plot.XAxis.Dims.PxPerUnit) - (_plotToManage.Plot.GetAxisLimits().XMin * _plotToManage.Plot.XAxis.Dims.PxPerUnit) + ((_plotToManage.Plot.GetAxisLimits().XSpan * _plotToManage.Plot.XAxis.Dims.PxPerUnit) / 75);
            annotation.Y = (_plotToManage.Plot.GetAxisLimits().YMax * _plotToManage.Plot.YAxis.Dims.PxPerUnit) - (pointY * _plotToManage.Plot.YAxis.Dims.PxPerUnit) - ((_plotToManage.Plot.GetAxisLimits().YSpan * _plotToManage.Plot.YAxis.Dims.PxPerUnit) / 75);

            
            annotation.Label = abilities[pointIndex];

            pointSelected[name] = pointIndex;
            if (!previousPointSelected.ContainsKey(name))
                previousPointSelected[name] = pointIndex;
        }
        private List<ParsedLogEntry> GetCorrectData(PlotType type, Combat combatToPlot)
        {
            switch (type)
            {
                case PlotType.DamageOutput:
                    return combatToPlot.OutgoingDamageLogs;
                case PlotType.DamageTaken:
                    return combatToPlot.IncomingDamageLogs;
                case PlotType.HealingOutput:
                    return combatToPlot.OutgoingHealingLogs;
                case PlotType.HealingTaken:
                    return combatToPlot.IncomingHealingLogs;
                case PlotType.SheildedDamageTaken:
                    return combatToPlot.IncomingSheildedLogs;

            }
            return null;
        }

        private void AddSeries(PlotType type, string name, Color color, bool hasEffective = false)
        {
            var series = new CombatMetaDataSeries();
            series.Type = type;
            series.Name = name;
            series.Color = color;
            var legend = new LegendItemViewModel();
            legend.Name = series.Name;
            legend.Color = series.Color;
            legend.LegenedToggled += series.LegenedToggled;
            legend.HasEffective = hasEffective;
            if (hasEffective)
            { 
                series.EffectiveAnnotation = _plotToManage.Plot.PlotAnnotation("test", 0, 0, fontSize: 25, lineColor: series.Color, fillColor: Color.LightGray, fontColor: Color.WhiteSmoke);
                series.EffectiveAnnotation.IsVisible = false;
            }
            series.Legend = legend;
            series.Annotation = _plotToManage.Plot.PlotAnnotation("test", 0, 0, fontSize: 25, lineColor: series.Color, fillColor: Color.LightGray, fontColor: Color.WhiteSmoke);
            series.Annotation.IsVisible = false;
            
            series.TriggerRender += () =>
            {
                Dispatcher.CurrentDispatcher.Invoke(() => {
                    _plotToManage.Plot.AxisAuto();
                    _plotToManage.Render();
                });
            };
            _seriesToPlot.Add(series);
        }

    }
}
