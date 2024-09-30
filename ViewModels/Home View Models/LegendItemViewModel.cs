using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Color = ScottPlot.Color;

namespace SWTORCombatParser.ViewModels.Home_View_Models
{
    public class LegendItemViewModel : INotifyPropertyChanged
    {
        public event Action<bool, bool> LegenedToggled = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; set; }
        public Color Color { get; set; }
        public SolidColorBrush LegendColor => new(Avalonia.Media.Color.FromArgb(255, Color.R, Color.G, Color.B));
        public bool ShowEffective => HasEffective ? true : false;
        public bool HasEffective { get; set; }
        private bool _checked = true;
        public bool Checked
        {
            get => _checked; set
            {
                _checked = value;
                LegenedToggled(value, EffectiveChecked);
            }
        }
        private bool _effectiveChecked = false;
        public bool EffectiveChecked
        {
            get => _effectiveChecked; set
            {
                _effectiveChecked = value;
                LegenedToggled(Checked, value);
            }
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
