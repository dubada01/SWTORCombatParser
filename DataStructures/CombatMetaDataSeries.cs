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

            TriggerRender.InvokeSafely(arg1 || arg2);
        }
    }
}
