using SWTORCombatParser.ViewModels.Home_View_Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ScottPlot;
using ScottPlot.Plottables;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.DataStructures
{
    public class CombatMetaDataSeries : INotifyPropertyChanged
    {
        public event Action<bool> TriggerRender = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public LegendItemViewModel Legend { get; set; }
        public Dictionary<DateTime, Callout> Tooltip { get; internal set; } = new Dictionary<DateTime, Callout>();
        public Dictionary<DateTime, Callout> EffectiveTooltip { get; internal set; } = new Dictionary<DateTime, Callout>();
        public Dictionary<DateTime, Scatter> Points { get; set; } = new Dictionary<DateTime, Scatter>();
        public Dictionary<DateTime, Scatter> Line { get; set; } = new Dictionary<DateTime, Scatter>();
        public Dictionary<string, Scatter> LineByCharacter { get; set; } = new Dictionary<string, Scatter>();
        public Dictionary<string, Scatter> PointsByCharacter { get; set; } = new Dictionary<string, Scatter>();
        public Dictionary<DateTime, Scatter> EffectivePoints { get; set; } = new Dictionary<DateTime, Scatter>();
        public Dictionary<DateTime, Scatter> EffectiveLine { get; set; } = new Dictionary<DateTime, Scatter>();

        public PlotType Type { get; internal set; }
        public Dictionary<DateTime, List<(string, string)>> Abilities { get; internal set; } = new Dictionary<DateTime, List<(string, string)>>();

        public string Name;

        public Color Color;
        private bool _showPoints;
        private bool _showTrends = true;
        public void SetDisplayMode(bool points, bool trends)
        {
            _showPoints = points; _showTrends = trends;
            ApplyVisibility();
        }
        private void ApplyVisibility()
        {
            foreach (var plot in Points.Values) plot.IsVisible = Legend.Checked && _showPoints;
            foreach (var plot in Line.Values) plot.IsVisible = Legend.Checked && _showTrends;
            foreach (var plot in EffectivePoints.Values) plot.IsVisible = Legend.EffectiveChecked && _showPoints;
            foreach (var plot in EffectiveLine.Values) plot.IsVisible = Legend.EffectiveChecked && _showTrends;
            foreach (var tooltip in Tooltip.Values) tooltip.IsVisible = false;
            foreach (var tooltip in EffectiveTooltip.Values) tooltip.IsVisible = false;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public void Reset()
        {
            EffectiveLine.Clear();
            Tooltip.Clear();
            EffectiveTooltip.Clear();
            EffectivePoints.Clear();
            Points.Clear();
            PointsByCharacter.Clear();
            Line.Clear();
            LineByCharacter.Clear();
            Abilities.Clear();
        }
        internal void LegenedToggled(bool arg1, bool arg2)
        {
            ApplyVisibility();
            TriggerRender.InvokeSafely(arg1 || arg2);
        }
    }
}
