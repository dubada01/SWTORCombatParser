using ScottPlot;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Plotting;

namespace SWTORCombatParser.ViewModels.BattleReview
{

    public class ReviewPlotViewModel
    {
        private Combat _currentCombat;
        private DateTime _startTime;
        private DisplayType _typeSelected;
        private float _duration => (float)(_currentCombat.EndTime - _startTime).TotalSeconds;
        private List<Entity> _viewingEntities = new List<Entity>();
        private int _windowSize;
        public WpfPlot Plot { get; set; }
        public event Action<double> OnNewOffset = delegate { };
        public event Action<double, double> NewAxisLmits = delegate { };
        public ReviewPlotViewModel()
        {
            Plot = new WpfPlot();
            Plot.Plot.Style(dataBackground: Color.FromArgb(150, 10, 10, 10), figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 40, 40, 40));
            Plot.Plot.XAxis.Label(label: "Duration (s)");
            Plot.Plot.YAxis.Label(label: "Value");
            Plot.AxesChanged += OnAxisUpdated;
        }

        private void OnAxisUpdated(object sender, EventArgs e)
        {
            var min = Plot.Plot.XAxis.Dims.Min;
            var max = Plot.Plot.XAxis.Dims.Max;
            NewAxisLmits(min, max);
        }

        public void DisplayDataForCombat(Combat combat)
        {
            _currentCombat = combat;
            _startTime = _currentCombat.StartTime;
            
            UpdatePlot();
            Plot.Plot.SetAxisLimitsX(0, _duration);
            Plot.Refresh();
        }
        public void SetWindowSize(int windowSize)
        {
            _windowSize = windowSize;
        }
        public void SetDisplayType(DisplayType type)
        {
            _typeSelected = type;
        }
        public void SetViewableEntities(List<Entity> entitiesToshow)
        {
            _viewingEntities = entitiesToshow;
        }
        private void UpdatePlot()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Plot.Plot.Clear();
                foreach (var entitiy in _viewingEntities)
                {
                    var relaventData = GetReleventLogs(entitiy);
                    if (relaventData.Count == 0)
                        continue;
                    var xVals = PlotMaker.GetPlotXValsRates(PlotMaker.GetPlotXVals(relaventData, _startTime));
                    var yVals = PlotMaker.GetPlotYValRates(PlotMaker.GetPlotYVals(relaventData, true), PlotMaker.GetPlotXVals(relaventData, _startTime), _windowSize);
                    Color? dotColor = null;
                    if (xVals.Count() > 0)
                    {
                        var plt = Plot.Plot.AddScatter(xVals, yVals, markerShape: MarkerShape.none, lineWidth: 5, label: entitiy.Name + $" ({_windowSize}s average)");
                        dotColor = plt.Color.Lerp(Color.White, 0.25f);
                    }
                    var xPoints = PlotMaker.GetPlotXVals(relaventData, _startTime);
                    var yPoints = PlotMaker.GetPlotYVals(relaventData, true);
                    Plot.Plot.AddScatter(xPoints, yPoints, markerShape: MarkerShape.filledCircle, markerSize:10, lineStyle: LineStyle.None, color: dotColor, label: entitiy.Name);
                }
                Plot.Plot.Legend();
                Plot.Refresh();
                OnNewOffset(Plot.Plot.XAxis.Dims.DataOffsetPx);
            });
        }
        private List<ParsedLogEntry> GetReleventLogs(Entity entity)
        {
            switch (_typeSelected)
            {
                case DisplayType.Damage:
                    return _currentCombat.OutgoingDamageLogs[entity];
                case DisplayType.DamageTaken:
                    return _currentCombat.IncomingDamageLogs[entity];
                case DisplayType.Healing:
                    return _currentCombat.OutgoingHealingLogs[entity];
                case DisplayType.HealingReceived:
                    return _currentCombat.IncomingHealingLogs[entity];
                default:
                    return new List<ParsedLogEntry>();
            }
        }
    }
}
