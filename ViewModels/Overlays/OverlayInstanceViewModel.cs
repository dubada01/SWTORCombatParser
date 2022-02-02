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

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayInstanceViewModel : INotifyPropertyChanged
    {
        private object _refreshLock = new object();
        public bool OverlaysMoveable { get; set; }
        public string OverlayTypeImage { get; set; } = "../../resources/SwtorLogo_opaque.png";
        public ObservableCollection<OverlayMetricInfo> MetricBars { get; set; } = new ObservableCollection<OverlayMetricInfo>();
        public OverlayType Type { get; set; }
        public OverlayType SecondaryType { get; set; }
        public bool HasLeaderboard => Type == OverlayType.DPS || Type == OverlayType.EHPS || (Type == OverlayType.ShieldAbsorb && SecondaryType == OverlayType.DamageAvoided) || Type == OverlayType.HPS;
        public bool AddSecondaryToValue { get; set; } = false;
        public event Action<OverlayInstanceViewModel> OverlayClosed = delegate { };
        public event Action CloseRequested = delegate { };
        public event Action<bool> OnLocking = delegate { };
        public event Action<string> OnCharacterDetected = delegate { };
        public void OverlayClosing()
        {
            OverlayClosed(this);
        }
        public void RequestClose()
        {
            CloseRequested();
        }
        public OverlayInstanceViewModel(OverlayType type)
        {
            Type = type;
            if(Type == OverlayType.EHPS)
            {
                SecondaryType = OverlayType.Shielding;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.HPS)
            {
                SecondaryType = OverlayType.Shielding;
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
                AddSecondaryToValue = false;
            }
            if (Type == OverlayType.Mitigation)
            {
                Type = OverlayType.ShieldAbsorb;
                SecondaryType = OverlayType.DamageAvoided;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.ShieldAbsorb)
            {
                SecondaryType = OverlayType.DamageAvoided;
                AddSecondaryToValue = true;
            }
            CombatSelectionMonitor.NewCombatSelected += Refresh;
            CombatIdentifier.NewCombatStarted += Reset;
            CombatIdentifier.NewCombatAvailable += UpdateMetrics;
            Leaderboards.LeaderboardStandingsAvailable += UpdateStandings;
            Leaderboards.TopLeaderboardEntriesAvailable += UpdateTopEntries;
            Leaderboards.LeaderboardTypeChanged += UpdateLeaderboardType;
        }

        private void UpdateLeaderboardType(LeaderboardType obj)
        {
            if (obj == LeaderboardType.AllDiciplines)
                OverlayTypeImage = "../../resources/SwtorLogo_opaque.png";
            else
            {
                OverlayTypeImage = "../../resources/LocalPlayerIcon.png";
            }
            OnPropertyChanged("OverlayTypeImage");
        }

        private void UpdateTopEntries(Dictionary<LeaderboardEntryType, (string, double)> obj)
        {
            UpdateLeaderboardValues(obj);
        }

        private void UpdateStandings(Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>> obj)
        {
            UpdateLeaderboardTopEntries(obj);
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
                if (Type == OverlayType.ShieldAbsorb && SecondaryType == OverlayType.DamageAvoided && mitigationValues.Value.Item1 != null)
                    AddLeaderboardBar(mitigationValues.Value.Item1, mitigationValues.Value.Item2);
            }
        }

        internal void CharacterDetected(string name)
        {
            OnCharacterDetected(name);
        }

        public void Reset()
        {
            Leaderboards.Reset();
            ResetMetrics();
        }
        public void Refresh(Combat comb)
        {
            lock (_refreshLock)
            {
                ResetMetrics();
                UpdateMetrics(comb);
            }
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
            var metricbar = new OverlayMetricInfo
            {
                Type = Type,
                Player = new Entity { Name = characterName },
                Value = value,
                IsLeaderboardValue = true,
                RelativeLength = 1,
                MedalIconPath = "../../resources/firstPlaceLeaderboardIcon.png"
            };
            App.Current.Dispatcher.Invoke(() => {
                MetricBars.Add(metricbar);
            });
            OrderMetricBars();
        }
        private void AddLeaderboardStanding(OverlayMetricInfo metricToUpdate, Dictionary<LeaderboardEntryType,(double,bool)> standings)
        {
            (double, bool) leaderboardRanking = (0,false);
            if (Type == OverlayType.DPS)
            {
                leaderboardRanking = standings[LeaderboardEntryType.Damage];
            }
            if (Type == OverlayType.FocusDPS)
            {
                leaderboardRanking = standings[LeaderboardEntryType.FocusDPS];

            }
            if (Type == OverlayType.EHPS)
            {
                leaderboardRanking = standings[LeaderboardEntryType.EffectiveHealing];
            }
            if (Type == OverlayType.HPS)
            {
                leaderboardRanking = standings[LeaderboardEntryType.Healing];
            }
            if (Type == OverlayType.ShieldAbsorb && SecondaryType == OverlayType.DamageAvoided)
            {
                leaderboardRanking = standings[LeaderboardEntryType.Mitigation];
            }
            metricToUpdate.LeaderboardRank = leaderboardRanking.Item1.ToString();
            metricToUpdate.RankIsPersonalRecord = leaderboardRanking.Item2;
        }
        private void UpdateMetrics(Combat obj)
        {
            RefreshBarViews(obj);
        }
        private void RefreshBarViews(Combat combatToDisplay)
        {

            OverlayMetricInfo metricToUpdate;
            if (combatToDisplay.CharacterParticipants.Count == 0)
                return;
            foreach (var participant in combatToDisplay.CharacterParticipants)
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

                UpdateMetric(Type, metricToUpdate, combatToDisplay, participant);
                if (SecondaryType != OverlayType.None)
                {
                    UpdateSecondary(SecondaryType, metricToUpdate, combatToDisplay, participant);
                }

                OrderMetricBars();

            }

        }
        private void OrderMetricBars()
        {
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
        private void UpdateLeaderboardTopEntries(Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>> leaderboardInfo)
        {
            foreach (var metricBar in MetricBars)
            {
                metricBar.LeaderboardRank = "0";
                metricBar.RankIsPersonalRecord = false;
                var participant = metricBar.Player;
                if (HasLeaderboard && leaderboardInfo.ContainsKey(participant) && leaderboardInfo[participant] != null)
                {
                    AddLeaderboardStanding(metricBar, leaderboardInfo[participant]);
                }
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
            double value = GetValueForMetric(type, combat, participant);
            metric.SecondaryType = type;
            metric.SecondaryValue = value;
        }
        private void UpdateMetric(OverlayType type, OverlayMetricInfo metricToUpdate, Combat obj, Entity participant)
        {
            var value = GetValueForMetric(type, obj, participant);
            metricToUpdate.Value = value;
        }
        private static double GetValueForMetric(OverlayType type, Combat combat, Entity participant)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.APM:
                    value = combat.APM[participant];
                    break;
                case OverlayType.DPS:
                    value = combat.ERegDPS[participant];
                    break;
                case OverlayType.EHPS:
                    value = combat.EHPS[participant];
                    break;
                case OverlayType.Shielding:
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
                case OverlayType.Mitigation:
                    value = combat.MPS[participant];
                    break;
                case OverlayType.DamageAvoided:
                    value = combat.DAPS[participant];
                    break;
                case OverlayType.ShieldAbsorb:
                    value = combat.SAPS[participant];
                    break;
                case OverlayType.BurstDPS:
                    value = combat.MaxBurstDamage[participant];
                    break;
                case OverlayType.BurstEHPS:
                    value = combat.MaxBurstHeal[participant];
                    break;
                case OverlayType.BurstDamageTaken:
                    value = combat.MaxBurstDamageTaken[participant];
                    break;
                case OverlayType.HealReactionTime:
                    value = combat.AverageDamageRecoveryTimeTotal[participant];
                    break;
                case OverlayType.InterruptCount:
                    value = combat.TotalInterrupts[participant];
                    break;
            }
            return value;
        }



        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
