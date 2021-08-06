using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace SWTORCombatParser.Plotting
{
    public class LegendItemViewModel : INotifyPropertyChanged
    {
        public event Action<bool,bool> LegenedToggled = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;
        public string Name { get; set; }
        public Color Color { get; set; }
        public System.Windows.Media.Brush LegendColor => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255,Color.R,Color.G,Color.B));
        public Visibility ShowEffective => HasEffective ? Visibility.Visible : Visibility.Hidden;
        public bool HasEffective { get; set; }
        private bool _checked = true;
        public bool Checked { get => _checked; set {
                _checked = value;
                LegenedToggled(value, EffectiveChecked);
            } 
        }
        private bool _effectiveChecked = false;
        public bool EffectiveChecked { get => _effectiveChecked;set {
                _effectiveChecked = value;
                LegenedToggled(Checked, value);
            } }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
