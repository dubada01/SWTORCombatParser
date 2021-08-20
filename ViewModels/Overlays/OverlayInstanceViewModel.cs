using MoreLinq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayMetricInfo : INotifyPropertyChanged
    {
        private string playerName;
        private double relativeLength;
        private string _value;

        public string PlayerName { get => playerName; set {
                playerName = value;
                OnPropertyChanged();
            } }
        public double RelativeLength { get => relativeLength; set {
                relativeLength = value;
                OnPropertyChanged();
            } }
        public string Value { get => _value; set {
                _value = value;
                OnPropertyChanged();
            } }

        public OverlayType Type { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class OverlayInstanceViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<OverlayMetricInfo> MetricBars { get; set; } = new ObservableCollection<OverlayMetricInfo>();
        public OverlayType Type { get; set; }
        public OverlayInstanceViewModel(OverlayType type)
        {
            Type = type;
            CombatIdentifier.NewCombatAvailable += UpdateMetrics;
        }

        private void UpdateMetrics(Combat obj)
        {
            OverlayMetricInfo metricToUpdate;
            if(MetricBars.Any(m=>m.PlayerName == obj.CharacterName))
            {
                metricToUpdate = MetricBars.First(mb => mb.PlayerName == obj.CharacterName);
            }
            else
            {
                metricToUpdate = new OverlayMetricInfo() { PlayerName = obj.CharacterName, Type = Type };
                App.Current.Dispatcher.Invoke(() => {
                    MetricBars.Add(metricToUpdate);
                });
                
            }
            UpdateMetric(Type, metricToUpdate,obj);
            var maxValue = MetricBars.MaxBy(m => double.Parse(m.Value)).First().Value;
            foreach(var metric in MetricBars)
            {
                metric.RelativeLength = 110 * (double.Parse(metric.Value) / double.Parse(maxValue));
            }
            OnPropertyChanged("MetricBars");
        }
        private void UpdateMetric(OverlayType type, OverlayMetricInfo metricToUpdate, Combat obj)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.DPS:
                    value = obj.DPS;
                    break;
                case OverlayType.HPS:
                    value = obj.EHPS;
                    break;
            }
            metricToUpdate.Value = value.ToString("#,##0");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
