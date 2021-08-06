using ScottPlot.Plottable;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.Plotting
{
    public class CombatMetaDataSeries : INotifyPropertyChanged
    {
        public event Action TriggerRender = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public LegendItemViewModel Legend { get; set; }
        public Annotation Annotation { get; internal set; }
        public ScatterPlot Points { get; set; }
        public ScatterPlot Line { get; set; }
        public ScatterPlot EffectivePoints { get; set; }
        public ScatterPlot EffectiveLine { get; set; }
        public PlotType Type { get; internal set; }
        public List<string> Abilities { get; internal set; }
        public Annotation EffectiveAnnotation { get; internal set; }

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
                Points.IsVisible = true;
                Line.IsVisible = true;
            }
            else
            {
                Points.IsVisible = false;
                Line.IsVisible = false;
            }
            if (Legend.HasEffective)
            {
                if (arg2)
                {
                    EffectivePoints.IsVisible = true;
                    EffectiveLine.IsVisible = true;
                }
                else
                {
                    EffectivePoints.IsVisible = false;
                    EffectiveLine.IsVisible = false;
                }
            }

            TriggerRender();
        }
    }
}
