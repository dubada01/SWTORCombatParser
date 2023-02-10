//using MoreLinq;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayInstanceViewModel : INotifyPropertyChanged
    {
        private object _refreshLock = new object();
        private double sizeScalar = 1d;
        private string metricTotal;

        public bool OverlaysMoveable { get; set; }
        public string OverlayTypeImage { get; set; } = "../../resources/SwtorLogo_opaque.png";
        public bool UsingLeaderboard { get; set; }
        public double SizeScalar
        {
            get => sizeScalar; set
            {
                sizeScalar = value;
                foreach(var bar in _metricBarsDict)
                {
                    bar.Value.SizeScalar = sizeScalar;
                }

                OnPropertyChanged();
            }
        }
        public ConcurrentDictionary<(string,bool),OverlayMetricInfo> _metricBarsDict { get; set; } = new ConcurrentDictionary<(string, bool), OverlayMetricInfo>();
        public List<OverlayMetricInfo> MetricBars { get; set; } = new List<OverlayMetricInfo>();
        public OverlayType CreatedType { get; set; }
        public OverlayType Type { get; set; }
        public OverlayType SecondaryType { get; set; }
        public string MetricTotal
        {
            get => metricTotal; set
            {
                metricTotal = value;
                OnPropertyChanged();
            }
        }
        public bool HasLeaderboard => (Type == OverlayType.DPS || Type == OverlayType.EHPS || (Type == OverlayType.ShieldAbsorb && SecondaryType == OverlayType.DamageAvoided) || Type == OverlayType.HPS || Type == OverlayType.FocusDPS) && UsingLeaderboard;
        public GridLength LeaderboardRowHeight => HasLeaderboard ? new GridLength(20) : new GridLength(0);
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
            CreatedType = type;
            Type = type;
            if (Type == OverlayType.EHPS)
            {
                SecondaryType = OverlayType.ProvidedAbsorb;
                AddSecondaryToValue = true;
            }
            if (Type == OverlayType.HPS)
            {
                SecondaryType = OverlayType.ProvidedAbsorb;
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
            UpdateLeaderboardType(LeaderboardSettings.ReadLeaderboardSettings());
        }

        private void UpdateLeaderboardType(LeaderboardType obj)
        {
            if(obj == LeaderboardType.Off)
            {
                UsingLeaderboard = false;
            }
            else
            {
                UsingLeaderboard = true;
            }
            if (obj == LeaderboardType.AllDiciplines)
                OverlayTypeImage = "../../resources/SwtorLogo_opaque.png";
            else
            {
                OverlayTypeImage = "../../resources/LocalPlayerIcon.png";
            }
            OnPropertyChanged("OverlayTypeImage");
            OnPropertyChanged("HasLeaderboard");
        }

        private void UpdateTopEntries(Dictionary<LeaderboardEntryType, (string, double)> obj)
        {
            foreach(var entry in _metricBarsDict)
            {
                if(entry.Key.Item2)
                    _metricBarsDict.TryRemove(entry.Key, out var ignore);
            }
            OnPropertyChanged("MetricBars");
            if (obj.Count == 0)
            {
                OrderMetricBars();
                return; 
            }
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
            if (comb == null)
                return;
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
            if (_metricBarsDict.Keys.Any(mb => mb.Item2))
                return;
            var metricbar = new OverlayMetricInfo
            {
                Type = Type,
                Player = new Entity { Name = characterName },
                Value = value,
                IsLeaderboardValue = true,
                RelativeLength = 1,
                SizeScalar = SizeScalar,
                MedalIconPath = "../../resources/firstPlaceLeaderboardIcon.png"
            };
            _metricBarsDict.TryAdd((characterName,true),metricbar);
            OrderMetricBars();
        }
        private void AddLeaderboardStanding(OverlayMetricInfo metricToUpdate, Dictionary<LeaderboardEntryType, (double, bool)> standings)
        {
            (double, bool) leaderboardRanking = (0, false);
            if (Type == OverlayType.DPS && standings.ContainsKey(LeaderboardEntryType.Damage))
            {
                leaderboardRanking = standings[LeaderboardEntryType.Damage];
            }
            if (Type == OverlayType.FocusDPS && standings.ContainsKey(LeaderboardEntryType.FocusDPS))
            {
                leaderboardRanking = standings[LeaderboardEntryType.FocusDPS];

            }
            if (Type == OverlayType.EHPS && standings.ContainsKey(LeaderboardEntryType.EffectiveHealing))
            {
                leaderboardRanking = standings[LeaderboardEntryType.EffectiveHealing];
            }
            if (Type == OverlayType.HPS && standings.ContainsKey(LeaderboardEntryType.Healing))
            {
                leaderboardRanking = standings[LeaderboardEntryType.Healing];
            }
            if (Type == OverlayType.ShieldAbsorb && SecondaryType == OverlayType.DamageAvoided && standings.ContainsKey(LeaderboardEntryType.Mitigation))
            {
                leaderboardRanking = standings[LeaderboardEntryType.Mitigation];
            }
            metricToUpdate.LeaderboardRank = leaderboardRanking.Item1.ToString();
            metricToUpdate.RankIsPersonalRecord = leaderboardRanking.Item2;
        }
        private void UpdateMetrics(Combat obj)
        {
            RefreshBarViews(obj);
            double sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value);
            if(CreatedType == OverlayType.DPS || CreatedType == OverlayType.EHPS || CreatedType == OverlayType.Mitigation)
                sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value + b.Value.SecondaryValue);
            if (CreatedType == OverlayType.DamageTaken)
                sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value - b.Value.SecondaryValue);

            MetricTotal = sum.ToString("N0");
        }
        private void RefreshBarViews(Combat combatToDisplay)
        {

            OverlayMetricInfo metricToUpdate;
            if (combatToDisplay.CharacterParticipants.Count == 0)
                return;
            foreach (var participant in combatToDisplay.CharacterParticipants)
            {
                if (_metricBarsDict.Any(m => m.Key.Item1 == participant.Name))
                {
                    metricToUpdate = _metricBarsDict.FirstOrDefault(mb => mb.Key.Item1 == participant.Name && !mb.Key.Item2).Value;
                    if (metricToUpdate == null)
                        continue;
                }
                else
                {
                    metricToUpdate = new OverlayMetricInfo() { Player = participant, Type = Type, AddSecondayToValue = AddSecondaryToValue, SizeScalar = SizeScalar};
                    _metricBarsDict.TryAdd((participant.Name,false),metricToUpdate);
                }

                UpdateMetric(Type, metricToUpdate, combatToDisplay, participant);
                if (SecondaryType != OverlayType.None)
                {
                    UpdateSecondary(SecondaryType, metricToUpdate, combatToDisplay, participant);
                }
            }
            OrderMetricBars();
        }
        private void OrderMetricBars()
        {
            if (!_metricBarsDict.Any())
                return;
            try
            {
                var maxValue = _metricBarsDict.MaxBy(m => double.Parse(m.Value.TotalValue, CultureInfo.InvariantCulture)).Value.TotalValue;
                foreach (var metric in _metricBarsDict)
                {
                    if (double.Parse(metric.Value.TotalValue, CultureInfo.InvariantCulture) == 0 || (metric.Value.Value + metric.Value.SecondaryValue == 0) || double.IsInfinity(metric.Value.Value) || double.IsNaN(metric.Value.Value))
                        metric.Value.RelativeLength = 0;
                    else
                        metric.Value.RelativeLength = double.Parse(maxValue, CultureInfo.InvariantCulture) == 0 ? 0 : (double.Parse(metric.Value.TotalValue, CultureInfo.InvariantCulture) / double.Parse(maxValue, CultureInfo.InvariantCulture));
                }

                var listOfBars = new List<OverlayMetricInfo>();
                var keys = _metricBarsDict.Keys.ToList();
                for(var i = 0; i < keys.Count; i++)
                {
                    OverlayMetricInfo bar;
                    var worked = _metricBarsDict.TryGetValue(keys[i], out bar);
                    if(worked)
                        listOfBars.Add(bar);
                }

                MetricBars = new List<OverlayMetricInfo>(listOfBars.OrderByDescending(mb => mb.RelativeLength));

                OnPropertyChanged("MetricBars");

            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to order overlay metrics: "+ex.Message);
            }

        }
        private void UpdateLeaderboardTopEntries(Dictionary<Entity, Dictionary<LeaderboardEntryType, (double, bool)>> leaderboardInfo)
        {
            foreach (var metricBar in _metricBarsDict)
            {
                metricBar.Value.LeaderboardRank = "0";
                metricBar.Value.RankIsPersonalRecord = false;
                var participant = metricBar.Value.Player;
                if (leaderboardInfo.Count == 0)
                    continue;
                if (HasLeaderboard && leaderboardInfo.ContainsKey(participant) && leaderboardInfo[participant] != null)
                {
                    AddLeaderboardStanding(metricBar.Value, leaderboardInfo[participant]);
                }
            }
        }
        private void ResetMetrics()
        {
            _metricBarsDict.Clear();
            MetricBars.Clear();
            OnPropertyChanged("MetricBars");
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
                case OverlayType.NonEDPS:
                    value = combat.DPS[participant];
                    break;
                case OverlayType.EHPS:
                    value = combat.EHPS[participant];
                    break;
                case OverlayType.ProvidedAbsorb:
                    value = combat.PSPS[participant];
                    break;
                case OverlayType.FocusDPS:
                    value = combat.EFocusDPS[participant];
                    break;
                case OverlayType.ThreatPerSecond:
                    value = combat.TPS[participant];
                    break;
                case OverlayType.Threat:
                    value = combat.TotalThreat[participant];
                    break;
                case OverlayType.DamageTaken:
                    value = combat.DTPS[participant];
                    break;
                case OverlayType.Mitigation:
                    value = combat.MPS[participant];
                    break;
                case OverlayType.DamageSavedDuringCD:
                    value = combat.DamageSavedFromCDPerSecond[participant];
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
                    value = combat.NumberOfHighSpeedReactions[participant];
                    break;
                case OverlayType.HealReactionTimeRatio:
                    value = combat.NumberOfHighSpeedReactions[participant] / combat.AbilitiesActivated[participant].Count;
                    break;
                case OverlayType.TankHealReactionTime:
                    value = combat.AverageTankDamageRecoveryTimeTotal[participant];
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
