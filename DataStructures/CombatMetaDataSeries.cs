using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.Plotting
{
    public class CombatMetaDataSeries : INotifyPropertyChanged
    {
        public event Action<bool> TriggerRender = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public LegendItemViewModel Legend { get; set; }
        public Dictionary<DateTime, Tooltip> Tooltip { get; internal set; } = new Dictionary<DateTime, Tooltip>();
        public Dictionary<DateTime, Tooltip> EffectiveTooltip { get; internal set; }= new Dictionary<DateTime, Tooltip>();
        public Dictionary<DateTime, ScatterPlot> Points { get; set; } = new Dictionary<DateTime, ScatterPlot>();
        public Dictionary<DateTime, ScatterPlot> Line { get; set; } = new Dictionary<DateTime, ScatterPlot>();
        public Dictionary<DateTime, ScatterPlot> EffectivePoints { get; set; } = new Dictionary<DateTime, ScatterPlot>();
        public Dictionary<DateTime, ScatterPlot> EffectiveLine { get; set; } = new Dictionary<DateTime, ScatterPlot>();
        public PlotType Type { get; internal set; }
        public Dictionary<DateTime, List<(string,string)>> Abilities { get; internal set; } = new Dictionary<DateTime, List<(string, string)>>();
        

        public string Name;

        public Color Color;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void LegenedToggled(bool arg1, bool arg2)
        {
            if (Points == null)
                return;
            if (arg1)
            {
                Points.Values.ToList().ForEach(v => v.IsVisible = true);
                Line.Values.ToList().ForEach(v => v.IsVisible = true);
            }
            else
            {
                Points.Values.ToList().ForEach(v => v.IsVisible = false);
                Line.Values.ToList().ForEach(v => v.IsVisible = false);
            }
            if (Legend.HasEffective)
            {
                if (arg2)
                {
                    EffectivePoints.Values.ToList().ForEach(v => v.IsVisible = true);
                    EffectiveLine.Values.ToList().ForEach(v => v.IsVisible = true);
                }
                else
                {
                    EffectivePoints.Values.ToList().ForEach(v => v.IsVisible = false);
                    EffectiveLine.Values.ToList().ForEach(v => v.IsVisible = false);
                }
            }

            TriggerRender(arg1||arg2);
        }
    }
}
