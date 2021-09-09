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
        public Entity Player { get; set; }
        public string PlayerName => Player.Name;
        public double RelativeLength { get => relativeLength; set {
                
                if (double.IsNaN(relativeLength) || double.IsInfinity(relativeLength) || Value+SecondaryValue==0 || TotalValue=="0")
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
            if(Type == OverlayType.EHPS)
            {
                SecondaryType = OverlayType.SPS;
            }
            if (Type == OverlayType.DPS)
            {
                SecondaryType = OverlayType.FocusDPS;
            }
            CombatSelectionMonitor.NewCombatSelected += Refresh;
            //StaticRaidInfo.NewRaidCombatDisplayed += UpdateMetrics;
            //StaticRaidInfo.OnPlayerRemoved += RemovePlayer;
            CombatIdentifier.NewCombatAvailable += UpdateMetrics;
            //StaticRaidInfo.NewRaidCombatStarted += ResetMetrics;
        }
        public void Refresh(Combat comb)
        {
            ResetMetrics();
            UpdateMetrics(comb);
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
        private void RemovePlayer(string playerName)
        {
            var barToRemove = MetricBars.FirstOrDefault(mb => mb.PlayerName == playerName);
            if (barToRemove != null)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    MetricBars.Remove(barToRemove);
                });
            }
        }
        private void UpdateMetrics(Combat obj)
        {
            OverlayMetricInfo metricToUpdate;
            if (obj.CharacterParticipants.Count == 0)
                return;
            foreach(var participant in obj.CharacterParticipants)
            {
                if (MetricBars.Any(m => m.Player == participant))
                {
                    metricToUpdate = MetricBars.First(mb => mb.Player == participant);
                }
                else
                {
                    metricToUpdate = new OverlayMetricInfo() { Player = participant, Type = Type };
                    App.Current.Dispatcher.Invoke(() => {
                        MetricBars.Add(metricToUpdate);
                    });
                }

                UpdateMetric(Type, metricToUpdate, obj, participant);
                if (SecondaryType != OverlayType.None)
                {
                    UpdateSecondary(SecondaryType, metricToUpdate, obj,participant);
                }
                var maxValue = MetricBars.MaxBy(m => double.Parse(m.TotalValue)).First().TotalValue;
                foreach (var metric in MetricBars)
                {
                    if (double.Parse(metric.TotalValue) == 0 || (metric.Value + metric.SecondaryValue == 0) || double.IsInfinity(metric.Value) || double.IsNaN(metric.Value))
                        metric.RelativeLength = 0;
                    else
                        metric.RelativeLength = double.Parse(maxValue) == 0 ? 0 : (double.Parse(metric.TotalValue) / double.Parse(maxValue));
                }
                App.Current.Dispatcher.Invoke(() =>
                {
                    MetricBars = new ObservableCollection<OverlayMetricInfo>(MetricBars.OrderByDescending(mb => mb.RelativeLength));
                });
                OnPropertyChanged("MetricBars");
            }
            
        }
        private void ResetMetrics()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                MetricBars.Clear();
            });
        }
        private void UpdateSecondary(OverlayType type, OverlayMetricInfo metric, Combat combat, Entity participant)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.DPS:
                    value = combat.RegDPS[participant];
                    break;
                case OverlayType.EHPS:
                    value = combat.EHPS[participant];
                    break;
                //case OverlayType.SPS:
                //    //value = combat.PSPS[participant];
                //    break;
                case OverlayType.FocusDPS:
                    value = combat.FocusDPS[participant];
                    break;
                case OverlayType.TPS:
                    value = combat.TPS[participant];
                    break;
                case OverlayType.DTPS:
                    value = combat.DTPS[participant];
                    break;
                case OverlayType.CompanionDPS:
                    value = combat.CompDPS[participant];
                    break;
                case OverlayType.CompanionEHPS:
                    value = combat.CompEHPS[participant];
                    break;
            }
            metric.SecondaryType = type;
            metric.SecondaryValue = value;
        }
        private void UpdateMetric(OverlayType type, OverlayMetricInfo metricToUpdate, Combat obj, Entity participant)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.DPS:
                    value = obj.RegDPS[participant];
                    break;
                case OverlayType.FocusDPS:
                    value = obj.FocusDPS[participant];
                    break;
                case OverlayType.EHPS:
                    value = obj.EHPS[participant];
                    break;
                //case OverlayType.SPS:
                //    value = obj.PSPS[participant];
                //    break;
                case OverlayType.TPS:
                    value = obj.TPS[participant];
                    break;
                case OverlayType.DTPS:
                    value = obj.DTPS[participant];
                    break;
                case OverlayType.CompanionDPS:
                    value = obj.CompDPS[participant];
                    break;
                case OverlayType.CompanionEHPS:
                    value = obj.CompEHPS[participant];
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
