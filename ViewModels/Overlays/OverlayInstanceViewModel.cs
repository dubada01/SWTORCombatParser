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
        private double _value;
        private double _secondaryValue;
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }
        public GridLength SecondaryBarWidth { get; set; }
        public string PlayerName { get => playerName; set {
                playerName = value;
                OnPropertyChanged();
            } }
        public double RelativeLength { get => relativeLength; set {
                
                if (double.IsNaN(relativeLength) || double.IsInfinity(relativeLength) || Value==0)
                {
                    SetBarToZero();
                    return; 
                }
                relativeLength = value;
                if(SecondaryType!= OverlayType.None)
                {
                    var primaryFraction = Value / double.Parse(TotalValue, System.Globalization.NumberStyles.AllowThousands);
                    var secondaryFraction = SecondaryValue / double.Parse(TotalValue, System.Globalization.NumberStyles.AllowThousands);
                    BarWidth = new GridLength(relativeLength*primaryFraction, GridUnitType.Star);
                    SecondaryBarWidth = new GridLength(relativeLength * secondaryFraction, GridUnitType.Star);
                    RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                }
                else
                {
                    BarWidth = new GridLength(relativeLength, GridUnitType.Star);
                    SecondaryBarWidth = new GridLength(0, GridUnitType.Star);
                    RemainderWidth = new GridLength(1 - relativeLength, GridUnitType.Star);
                }
                OnPropertyChanged("RemainderWidth");
                OnPropertyChanged("BarWidth");
            } }
        private void SetBarToZero()
        {
            relativeLength = 0;
            BarWidth = new GridLength(0, GridUnitType.Star);
            RemainderWidth = new GridLength(1, GridUnitType.Star);
            OnPropertyChanged("RemainderWidth");
            OnPropertyChanged("BarWidth");
        }
        public double Value { get => _value; set {
                _value = value;
                if(double.IsNaN(_value))
                {

                }
                OnPropertyChanged();
            } }
        public double SecondaryValue
        {
            get => _secondaryValue; set
            {
                _secondaryValue = value;
                OnPropertyChanged();
            }
        }
        public string TotalValue => (Value + SecondaryValue).ToString("#,##0");
        public void Reset()
        {
            Value = 0;
            SecondaryValue = 0;
            RelativeLength = 0;
        }
        public OverlayType Type { get; set; }
        public OverlayType SecondaryType { get; set; }
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
        public OverlayType SecondaryType { get; set; }
        public event Action<OverlayInstanceViewModel> OverlayClosed = delegate { };
        public void OverlayClosing()
        {
            OverlayClosed(this);
        }
        public OverlayInstanceViewModel(OverlayType type)
        {
            Type = type;
            if(Type == OverlayType.Healing)
            {
                SecondaryType = OverlayType.Sheilding;
            }
            CombatSelectionMonitor.NewCombatSelected += UpdateMetrics;
            StaticRaidInfo.NewRaidCombatDisplayed += UpdateMetrics;
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
            if (string.IsNullOrEmpty(obj.CharacterName))
                return;
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
            if(SecondaryType!= OverlayType.None)
            {
                UpdateSecondary(SecondaryType, metricToUpdate, obj);
            }
            var maxValue = MetricBars.MaxBy(m => double.Parse(m.TotalValue)).First().TotalValue;
            foreach(var metric in MetricBars)
            {
                if (double.Parse(metric.TotalValue) == 0 || metric.Value == 0 || double.IsInfinity(metric.Value) || double.IsNaN(metric.Value))
                    metric.RelativeLength = 0;
                else
                    metric.RelativeLength = double.Parse(maxValue)==0?0:(double.Parse(metric.TotalValue) / double.Parse(maxValue));
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
        private void UpdateSecondary(OverlayType type, OverlayMetricInfo metric, Combat comabat)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.DPS:
                    value = comabat.DPS;
                    break;
                case OverlayType.Healing:
                    value = comabat.EHPS;
                    break;
                case OverlayType.Sheilding:
                    value = comabat.PSPS;
                    break;
                case OverlayType.Threat:
                    value = comabat.TPS;
                    break;
                case OverlayType.DTPS:
                    value = comabat.DTPS;
                    break;
            }
            metric.SecondaryType = type;
            metric.SecondaryValue = value;
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
                    value = obj.EHPS;
                    break;
                case OverlayType.Sheilding:
                    value = obj.PSPS;
                    break;
                case OverlayType.Threat:
                    value = obj.TPS;
                    break;
                case OverlayType.DTPS:
                    value = obj.DTPS;
                    break;
            }
            metricToUpdate.Value = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
