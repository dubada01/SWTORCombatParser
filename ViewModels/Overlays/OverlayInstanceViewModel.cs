using MoreLinq;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Overlays;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayMetricInfo : INotifyPropertyChanged
    {
        private double relativeLength;
        private double _value;
        private double _secondaryValue;
        private int leaderboardRank;
        public string MedalIconPath { get; set; }
        public string InfoText => $"{Type}: {(int)Value}" + (SecondaryType != OverlayType.None ? $"\n{SecondaryType}: {(int)SecondaryValue}" : "");
        public GridLength RemainderWidth { get; set; }
        public GridLength BarWidth { get; set; }
        public GridLength SecondaryBarWidth { get; set; }
        public double BorderThickness => IsLeaderboardValue ? 3 : 0;
        public CornerRadius BarRadius { get; set; } = new CornerRadius(3, 3, 3, 3);
        public CornerRadius BarRadiusSecondary { get; set; } = new CornerRadius(3, 3, 3, 3);
        public SolidColorBrush BarOutline => IsLeaderboardValue ? Brushes.NavajoWhite : Brushes.Transparent;
        public bool AddSecondayToValue { get; set; }
        public Entity Player { get; set; }
        public string LeaderboardRank
        {
            get => leaderboardRank == 0?"":leaderboardRank.ToString()+". ";
            set
            {
                leaderboardRank = int.Parse(value);
                OnPropertyChanged();
            }
        }
        public string PlayerName => Player.Name;
        public bool IsLeaderboardValue { get; set; } = false;


        public double RelativeLength
        {
            get => relativeLength;
            set
            {

                if (double.IsNaN(relativeLength) || double.IsInfinity(relativeLength) || Value + SecondaryValue == 0 || TotalValue == "0")
                {
                    SetBarToZero();
                    return;
                }
                relativeLength = value;
                if (SecondaryType != OverlayType.None)
                {
                    var primaryFraction = Value / double.Parse(TotalValue, System.Globalization.NumberStyles.AllowThousands);
                    var secondaryFraction = SecondaryValue / double.Parse(TotalValue, System.Globalization.NumberStyles.AllowThousands);
                    BarWidth = new GridLength(relativeLength * primaryFraction, GridUnitType.Star);
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
                BarRadius = SecondaryType == OverlayType.None || SecondaryValue == 0 ? new CornerRadius(3, 3, 3, 3) : new CornerRadius(3, 0, 0, 3);
                BarRadiusSecondary = SecondaryType == OverlayType.None ? new CornerRadius(3, 3, 3, 3) : new CornerRadius(0, 3, 3, 0);
                OnPropertyChanged("BarRadius");
                OnPropertyChanged("BarRadiusSecondary");
            }
        }
        private void SetBarToZero()
        {
            relativeLength = 0;
            BarWidth = new GridLength(0, GridUnitType.Star);
            RemainderWidth = new GridLength(1, GridUnitType.Star);
            OnPropertyChanged("RemainderWidth");
            OnPropertyChanged("BarWidth");
        }
        public double Value
        {
            get => _value; set
            {
                _value = value;
                if (double.IsNaN(_value))
                {

                }
                OnPropertyChanged();
            }
        }
        public double SecondaryValue
        {
            get => _secondaryValue; set
            {
                _secondaryValue = value;
                OnPropertyChanged();
            }
        }
        public string TotalValue => (Value + (AddSecondayToValue ? SecondaryValue : 0)).ToString("#,##0");
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
        private object _refreshLock = new object();
        private Combat _currentCombat;
        private Dictionary<Entity, Dictionary<LeaderboardEntryType, double>> _leaderboardInfo = new Dictionary<Entity, Dictionary<LeaderboardEntryType, double>>();
        public bool OverlaysMoveable { get; set; }
        public ObservableCollection<OverlayMetricInfo> MetricBars { get; set; } = new ObservableCollection<OverlayMetricInfo>();
        public OverlayType Type { get; set; }
        public OverlayType SecondaryType { get; set; }
        public bool HasLeaderboard => Type == OverlayType.DPS || Type == OverlayType.EHPS || (Type == OverlayType.SheildAbsorb && SecondaryType == OverlayType.DamageAvoided) || Type == OverlayType.HPS;
        public bool AddSecondaryToValue { get; set; } = false;
        public event Action<OverlayInstanceViewModel> OverlayClosed = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public void OverlayClosing()
        {
            OverlayClosed(this);
        }
        public OverlayInstanceViewModel(OverlayType type)
        {
            Type = type;
            if(Type == OverlayType.EHPS)
            {
                SecondaryType = OverlayType.Sheilding;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.HPS)
            {
                SecondaryType = OverlayType.Sheilding;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.DPS)
            {
                SecondaryType = OverlayType.FocusDPS;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.DamageTaken)
            {
                SecondaryType = OverlayType.Mitigation;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.Mitigation)
            {
                Type = OverlayType.SheildAbsorb;
                SecondaryType = OverlayType.DamageAvoided;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.SheildAbsorb)
            {
                SecondaryType = OverlayType.DamageAvoided;
                AddSecondaryToValue = true;
            }
            CombatSelectionMonitor.NewCombatSelected += Refresh;
            CombatIdentifier.NewCombatStarted += Reset;
            CombatIdentifier.NewCombatAvailable += UpdateMetrics;
            Leaderboards.LeaderboardStandingsAvailable += UpdateStandings;
            Leaderboards.TopLeaderboardEntriesAvailable += UpdateTopEntries;
        }

        private void UpdateTopEntries(Dictionary<LeaderboardEntryType, (string, double)> obj)
        {
            UpdateLeaderboardValues(obj);
            RefreshBarViews();
        }

        private void UpdateStandings(Dictionary<Entity, Dictionary<LeaderboardEntryType, double>> obj)
        {
            _leaderboardInfo = obj;
            RefreshBarViews();
        }

        private void UpdateLeaderboardValues(Dictionary<LeaderboardEntryType, (string, double)> obj)
        {
            lock (Leaderboards._updateLock)
            {

                var damageLeaderboardValues = obj.FirstOrDefault(kvp => kvp.Key == LeaderboardEntryType.Damage);
                var focusDamageValues = obj.FirstOrDefault(kvp => kvp.Key == LeaderboardEntryType.FocusDPS);
                var healingValues = obj.FirstOrDefault(kvp => kvp.Key == LeaderboardEntryType.Healing);
                var effectiveHealingValues = obj.FirstOrDefault(kvp => kvp.Key == LeaderboardEntryType.EffectiveHealing);
                var mitigationValues = obj.FirstOrDefault(kvp => kvp.Key == LeaderboardEntryType.Mitigation);
                if (Type == OverlayType.DPS && damageLeaderboardValues.Value.Item1 != null)
                    AddLeaderboardBar(damageLeaderboardValues.Value.Item1, damageLeaderboardValues.Value.Item2);
                if (Type == OverlayType.FocusDPS && focusDamageValues.Value.Item1 != null)
                    AddLeaderboardBar(focusDamageValues.Value.Item1, focusDamageValues.Value.Item2);
                if (Type == OverlayType.EHPS && effectiveHealingValues.Value.Item1 != null)
                    AddLeaderboardBar(effectiveHealingValues.Value.Item1, effectiveHealingValues.Value.Item2);
                if (Type == OverlayType.HPS && healingValues.Value.Item1 != null)
                    AddLeaderboardBar(healingValues.Value.Item1, healingValues.Value.Item2);
                if (Type == OverlayType.SheildAbsorb && SecondaryType == OverlayType.DamageAvoided && mitigationValues.Value.Item1 != null)
                    AddLeaderboardBar(mitigationValues.Value.Item1, mitigationValues.Value.Item2);
            }
        }

        public void Reset()
        {
            Leaderboards.Reset();
            ResetMetrics();
        }
        public void Refresh(Combat comb)
        {
            ResetMetrics();
            UpdateMetrics(comb);
        }
        public void LockOverlays()
        {
            OnLocking(true);
               OverlaysMoveable = false;
            OnPropertyChanged("OverlaysMoveable");
        }
        public void UnlockOverlays()
        {
            OnLocking(false);
            OverlaysMoveable = true;
            OnPropertyChanged("OverlaysMoveable");
        }
        private void AddLeaderboardBar(string characterName, double value)
        {
            if (MetricBars.Any(mb => mb.IsLeaderboardValue))
                return;
            App.Current.Dispatcher.Invoke(() => {
                MetricBars.Add(new OverlayMetricInfo
                {
                    Type = Type,
                    Player = new Entity { Name = characterName },
                    Value = value,
                    IsLeaderboardValue = true,
                    MedalIconPath = "../../resources/firstPlaceLeaderboardIcon.png"
                });
            });
        }
        private void AddLeaderboardStanding(OverlayMetricInfo metricToUpdate, Dictionary<LeaderboardEntryType,double> standings)
        {
            if (Type == OverlayType.DPS)
                metricToUpdate.LeaderboardRank = standings[LeaderboardEntryType.Damage].ToString();
            if (Type == OverlayType.FocusDPS)
                metricToUpdate.LeaderboardRank = standings[LeaderboardEntryType.FocusDPS].ToString();
            if (Type == OverlayType.EHPS)
                metricToUpdate.LeaderboardRank = standings[LeaderboardEntryType.EffectiveHealing].ToString();
            if (Type == OverlayType.HPS)
                metricToUpdate.LeaderboardRank = standings[LeaderboardEntryType.Healing].ToString();
            if (Type == OverlayType.SheildAbsorb && SecondaryType == OverlayType.DamageAvoided)
                metricToUpdate.LeaderboardRank = standings[LeaderboardEntryType.Mitigation].ToString();
        }
        private void UpdateMetrics(Combat obj)
        {
            _currentCombat = obj;
            RefreshBarViews();
            
        }
        private void RefreshBarViews()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                OverlayMetricInfo metricToUpdate;
                if (_currentCombat.CharacterParticipants.Count == 0)
                    return;
                foreach (var participant in _currentCombat.CharacterParticipants)
                {
                    if (MetricBars.Any(m => m.Player == participant))
                    {
                        metricToUpdate = MetricBars.First(mb => mb.Player == participant);
                    }
                    else
                    {
                        metricToUpdate = new OverlayMetricInfo() { Player = participant, Type = Type, AddSecondayToValue = AddSecondaryToValue };
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            MetricBars.Add(metricToUpdate);
                        });
                    }
                    if (_currentCombat.IsEncounterBoss && HasLeaderboard && _leaderboardInfo.ContainsKey(participant))
                    {
                        AddLeaderboardStanding(metricToUpdate, _leaderboardInfo[participant]);
                    }
                    UpdateMetric(Type, metricToUpdate, _currentCombat, participant);
                    if (SecondaryType != OverlayType.None)
                    {
                        UpdateSecondary(SecondaryType, metricToUpdate, _currentCombat, participant);
                    }
                    var maxValue = MetricBars.MaxBy(m => double.Parse(m.TotalValue)).First().TotalValue;
                    foreach (var metric in MetricBars)
                    {
                        if (double.Parse(metric.TotalValue) == 0 || (metric.Value + metric.SecondaryValue == 0) || double.IsInfinity(metric.Value) || double.IsNaN(metric.Value))
                            metric.RelativeLength = 0;
                        else
                            metric.RelativeLength = double.Parse(maxValue) == 0 ? 0 : (double.Parse(metric.TotalValue) / double.Parse(maxValue));
                    }


                    MetricBars = new ObservableCollection<OverlayMetricInfo>(MetricBars.OrderByDescending(mb => mb.RelativeLength));

                    OnPropertyChanged("MetricBars");
                }
            });
        }


        private void ResetMetrics()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                MetricBars.Clear();
            });
            _leaderboardInfo = new Dictionary<Entity, Dictionary<LeaderboardEntryType, double>>();
        }
        private void UpdateSecondary(OverlayType type, OverlayMetricInfo metric, Combat combat, Entity participant)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.DPS:
                    value = combat.ERegDPS[participant];
                    break;
                case OverlayType.EHPS:
                    value = combat.EHPS[participant];
                    break;
                case OverlayType.Sheilding:
                    value = combat.PSPS[participant];
                    break;
                case OverlayType.FocusDPS:
                    value = combat.FocusDPS[participant];
                    break;
                case OverlayType.Threat:
                    value = combat.TPS[participant];
                    break;
                case OverlayType.DamageTaken:
                    value = combat.DTPS[participant];
                    break;
                case OverlayType.CompanionDPS:
                    value = combat.CompDPS[participant];
                    break;
                case OverlayType.CompanionEHPS:
                    value = combat.CompEHPS[participant];
                    break;
                case OverlayType.Mitigation:
                    value = combat.MPS[participant];
                    break;
                case OverlayType.DamageAvoided:
                    value = combat.DAPS[participant];
                    break;
                case OverlayType.SheildAbsorb:
                    value = combat.SAPS[participant];
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
                    value = obj.ERegDPS[participant];
                    break;
                case OverlayType.FocusDPS:
                    value = obj.EFocusDPS[participant];
                    break;
                case OverlayType.EHPS:
                    value = obj.EHPS[participant];
                    break;
                case OverlayType.HPS:
                    value = obj.HPS[participant];
                    break;
                case OverlayType.Tank_Sheilding:
                    value = obj.SPS[participant];
                    break;
                case OverlayType.Threat:
                    value = obj.TPS[participant];
                    break;
                case OverlayType.DamageTaken:
                    value = obj.EDTPS[participant];
                    break;
                case OverlayType.CompanionDPS:
                    value = obj.CompDPS[participant];
                    break;
                case OverlayType.CompanionEHPS:
                    value = obj.CompEHPS[participant];
                    break;
                case OverlayType.PercentOfFightBelowFullHP:
                    value = obj.PercentageOfFightBelowFullHP[participant];
                    break;
                case OverlayType.InterruptCount:
                    value = obj.TotalInterrupts[participant];
                    break;
                case OverlayType.Mitigation:
                    value = obj.MPS[participant];
                    break;
                case OverlayType.DamageAvoided:
                    value = obj.DAPS[participant];
                    break;
                case OverlayType.SheildAbsorb:
                    value = obj.SAPS[participant];
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
