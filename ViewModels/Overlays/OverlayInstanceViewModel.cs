using MoreLinq;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayMetricInfo : INotifyPropertyChanged
    {
        private string playerName;
        private double relativeLength;
        private string _value;
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }
        public string PlayerName { get => playerName; set {
                playerName = value;
                OnPropertyChanged();
            } }
        public double RelativeLength { get => relativeLength; set {
                relativeLength = value;
                if (double.IsNaN(relativeLength))
                    return;
                BarWidth = new GridLength(relativeLength, GridUnitType.Star);
                RemainderWidth = new GridLength(1-relativeLength,GridUnitType.Star);
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
            } }
        public string Value { get => _value; set {
                _value = value;
                OnPropertyChanged();
            } }
        public void Reset()
        {
            Value = "0";
            RelativeLength = 0;
        }
        public OverlayType Type { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class OverlayInstanceViewModel : INotifyPropertyChanged
    {
        public bool OverlaysMoveable { get; set; }
        public ObservableCollection<OverlayMetricInfo> MetricBars { get; set; } = new ObservableCollection<OverlayMetricInfo>();
        public OverlayType Type { get; set; }
        public event Action<OverlayInstanceViewModel> OverlayClosed = delegate { };
        public void OverlayClosing()
        {
            OverlayClosed(this);
        }
        public OverlayInstanceViewModel(OverlayType type)
        {
            Type = type;
            CombatSelectionMonitor.NewCombatSelected += UpdateMetrics;
            CombatIdentifier.NewCombatAvailable += UpdateMetrics;
            StaticRaidInfo.NewRaidCombatStarted += ResetMetrics;
        }
        public void LockOverlays()
        {
            OverlaysMoveable = false;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OverlaysMoveable = true;
            OnPropertyChanged("OverlaysMoveable");
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

            UpdateMetric(Type, metricToUpdate, obj);
            var maxValue = MetricBars.MaxBy(m => double.Parse(m.Value)).First().Value;
            foreach(var metric in MetricBars)
            {
                metric.RelativeLength = (double.Parse(metric.Value) / double.Parse(maxValue));
            }
            App.Current.Dispatcher.Invoke(() =>
            {
                MetricBars = new ObservableCollection<OverlayMetricInfo>(MetricBars.OrderByDescending(mb => mb.RelativeLength));
            });
            OnPropertyChanged("MetricBars");
        }
        private void ResetMetrics()
        {
            foreach(var metric in MetricBars)
            {
                metric.Reset();
            }
        }
        private void UpdateMetric(OverlayType type, OverlayMetricInfo metricToUpdate, Combat obj)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.DPS:
                    value = obj.DPS;
                    break;
                case OverlayType.Healing:
                    value = obj.EHPS + obj.PSPS;
                    break;
                case OverlayType.Sheilding:
                    value = obj.TotalProvidedSheilding;
                    break;
                case OverlayType.Threat:
                    value = obj.TotalThreat;
                    break;
                case OverlayType.DTPS:
                    value = obj.DTPS;
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
