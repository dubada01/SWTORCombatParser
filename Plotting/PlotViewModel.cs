using ScottPlot;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;

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
        private MainWindow _gridView;
        private List<CombatMetaDataSeries> _seriesToPlot = new List<CombatMetaDataSeries>();
        public PlotViewModel(WpfPlot plotToManage, MainWindow window)
        {
            _gridView = window;
            _plotToManage = plotToManage;
        }
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
                        AddSeries(plotType, "Heal Incoming", Color.CornflowerBlue);
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
                series.Line = _plotToManage.Plot.AddScatter(plotXvals, plotYvalSums, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, label: series.Name + "/s", color: series.Color);
                series.Line.YAxisIndex = 2;
                if (series.Legend.HasEffective)
                {
                    var effectiveYVals = PlotMaker.GetPlotYVals(applicableData, series.Legend.HasEffective);
                    var effectiveYValSums = PlotMaker.GetPlotYValRates(applicableData, plotXvals, series.Legend.HasEffective);
                    
                    series.EffectivePoints = _plotToManage.Plot.AddScatter(plotXvals, effectiveYVals, lineStyle: LineStyle.None, markerShape: MarkerShape.openCircle, label: "Effective" + series.Name, color: Color.WhiteSmoke, markerSize: 15);
                    series.EffectiveLine = _plotToManage.Plot.AddScatter(plotXvals, effectiveYValSums, lineStyle: LineStyle.Solid, markerShape: MarkerShape.none, label: "Effective" + series.Name + "/s", color: Color.WhiteSmoke);
                    series.EffectiveLine.YAxisIndex = 2;
                    series.EffectivePoints.IsVisible = series.Legend.EffectiveChecked;
                    series.EffectiveLine.IsVisible = series.Legend.EffectiveChecked;
                }

                series.Points.IsVisible = series.Legend.Checked;
                series.Line.IsVisible = series.Legend.Checked;
            }
            _plotToManage.Plot.AxisAuto();
        }
        public ObservableCollection<InteractiveLegendViewModel> GetLegends()
        {
            return new ObservableCollection<InteractiveLegendViewModel>(_seriesToPlot.Select(s => s.Legend));
        }
        private Dictionary<string, int> pointSelected = new Dictionary<string, int>();
        private Dictionary<string, int> previousPointSelected = new Dictionary<string, int>();
        public void MousePositionUpdated()
        {
            foreach(var plot in _seriesToPlot)
            {
                if (plot.Points == null)
                    continue;
                (double mouseCoordX, double mouseCoordY) = _plotToManage.GetMouseCoordinates();
                double xyRatio = _plotToManage.Plot.XAxis.Dims.PxPerUnit / _plotToManage.Plot.YAxis.Dims.PxPerUnit;
                (double pointX, double pointY, int pointIndex) = plot.Points.GetPointNearest(mouseCoordX, mouseCoordY, xyRatio);

                var abilities = plot.Abilities;
                var annotation = plot.Annotation;
                annotation.X = (pointX * _plotToManage.Plot.XAxis.Dims.PxPerUnit) - (_plotToManage.Plot.GetAxisLimits().XMin * _plotToManage.Plot.XAxis.Dims.PxPerUnit) + ((_plotToManage.Plot.GetAxisLimits().XSpan * _plotToManage.Plot.XAxis.Dims.PxPerUnit) / 75);
                annotation.Y = (_plotToManage.Plot.GetAxisLimits().YMax * _plotToManage.Plot.YAxis.Dims.PxPerUnit) - (pointY * _plotToManage.Plot.YAxis.Dims.PxPerUnit) - ((_plotToManage.Plot.GetAxisLimits().YSpan * _plotToManage.Plot.YAxis.Dims.PxPerUnit) / 75);

                annotation.IsVisible = plot.Points.IsVisible;
                annotation.Label = abilities[pointIndex];
                pointSelected[plot.Name] = pointIndex;
                if (!previousPointSelected.ContainsKey(plot.Name))
                    previousPointSelected[plot.Name] = pointIndex;
            }
            if (previousPointSelected.Any(kvp => kvp.Value != pointSelected[kvp.Key]))
            {
                foreach (var key in pointSelected.Keys)
                    previousPointSelected[key] = pointSelected[key];
                _gridView.RenderPlot();
            }
        }
        private Color? LightenColor(Color color)
        {
            return Color.FromArgb((byte)(color.R *1.5f), (byte)(color.G * 1.5f), (byte)(color.B * 1.5f));
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
            var legend = new InteractiveLegendViewModel();
            legend.Name = series.Name;
            legend.Color = series.Color;
            legend.LegenedToggled += series.LegenedToggled;
            legend.HasEffective = hasEffective;
            series.Legend = legend;
            series.Annotation = _plotToManage.Plot.PlotAnnotation("test", 0, 0, fontSize: 25, lineColor: series.Color, fillColor: Color.LightGray, fontColor: Color.WhiteSmoke);
            series.Annotation.IsVisible = false;
            series.TriggerRender += () =>
            {
                _gridView.RenderAndResize();
            };
            _seriesToPlot.Add(series);
        }

    }
}
