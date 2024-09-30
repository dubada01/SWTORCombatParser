using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace SWTORCombatParser.ViewModels.CombatMetaData
{
    public class MetaDataInstance : INotifyPropertyChanged
    {
        public string Category { get; set; }
        public SolidColorBrush Color { get; set; }
        public string TotalLabel { get; set; }
        public string TotalValue { get; set; }
        public string MaxLabel { get; set; }
        public string MaxValue { get; set; }
        public string RateLabel { get; set; }
        public string RateValue { get; set; }
        public string EffectiveTotalLabel { get; set; }
        public string EffectiveTotalValue { get; set; }
        public string EffectiveMaxLabel { get; set; }
        public string EffectiveMaxValue { get; set; }
        public string EffectiveRateLabel { get; set; }
        public string EffectiveRateValue { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Reset()
        {
            TotalValue = "0";
            OnPropertyChanged("TotalValue");
            MaxValue = "0";
            OnPropertyChanged("MaxValue");
            RateValue = "0";
            OnPropertyChanged("RateValue");
            EffectiveMaxValue = "0";
            OnPropertyChanged("EffectiveMaxValue");
            EffectiveTotalValue = "0";
            OnPropertyChanged("EffectiveTotalValue");
            EffectiveRateValue = "0";
            OnPropertyChanged("EffectiveRateValue");
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
