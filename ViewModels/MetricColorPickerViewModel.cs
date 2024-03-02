using Prism.Commands;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels
{
    public class MetricColorPickerViewModel:INotifyPropertyChanged
    {

        private Color metricColor;
        private SolidColorBrush metricBrush;

        public OverlayType OverlayType { get; set; }
        public event Action CloseRequested = delegate { };
        public event PropertyChangedEventHandler PropertyChanged;

        public SolidColorBrush MetricBrush
        {
            get => metricBrush; set
            {
                metricBrush = value;
                OnPropertyChanged();
            }
        }
        public Color MetricColor
        {
            get => metricColor; set
            {
                metricColor = value;
                MetricColorLoader.SetColorForMetric(OverlayType, metricColor.ToString());
                MetricBrush = MetricColorLoader.CurrentMetricBrushDict[OverlayType];
            }
        }
        public MetricColorPickerViewModel(OverlayType type)
        {
            OverlayType = type;
            MetricColor = MetricColorLoader.GetMetricCurrentColor(type);
        }
        public ICommand SetDefaultCommand => new DelegateCommand(SetDefaultColor);

        private void SetDefaultColor()
        {
            MetricColor = MetricColorLoader.GetDefaultColorForMetric(OverlayType);
            OnPropertyChanged("MetricColor");
        }

        public ICommand CloseCommand => new DelegateCommand(CloseThis);

        private void CloseThis()
        {
            CloseRequested();
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
