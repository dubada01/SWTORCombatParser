//using MoreLinq;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Views;
using SWTORCombatParser.Views.Overlay;

namespace SWTORCombatParser.ViewModels.Overlays
{
    public class OverlayInstanceViewModel : BaseOverlayViewModel
    {
        private object _refreshLock = new object();
        private double sizeScalar = 1d;
        private string metricTotal;
        public override bool ShouldBeVisible => true;

        public Bitmap OverlayTypeImage
        {
            get => _overlayTypeImage;
            set => this.RaiseAndSetIfChanged(ref _overlayTypeImage, value);
        }

        public bool UsingLeaderboard
        {
            get => _usingLeaderboard;
            set => this.RaiseAndSetIfChanged(ref _usingLeaderboard, value);
        }

        public double SizeScalar
        {
            get => sizeScalar; set
            {
                this.RaiseAndSetIfChanged(ref sizeScalar, value);
                foreach (var bar in _metricBarsDict)
                {
                    bar.Value.SizeScalar = sizeScalar;
                }
            }
        }
        public ConcurrentDictionary<(string, bool), OverlayMetricInfo> _metricBarsDict { get; set; } = new ConcurrentDictionary<(string, bool), OverlayMetricInfo>();

        public ObservableCollection<OverlayMetricInfo> MetricBars
        {
            get => _metricBars;
            set => this.RaiseAndSetIfChanged(ref _metricBars, value);
        }

        public OverlayType CreatedType { get; set; }

        public OverlayType Type
        {
            get => _type;
            set => this.RaiseAndSetIfChanged(ref _type, value);
        }

        public OverlayType SecondaryType { get; set; }
        public string MetricTotal
        {
            get => metricTotal == "0" ? "" : metricTotal; set
            {
                this.RaiseAndSetIfChanged(ref metricTotal, value);
            }
        }
        public bool HasLeaderboard => (Type == OverlayType.DPS || Type == OverlayType.EHPS || (Type == OverlayType.ShieldAbsorb && SecondaryType == OverlayType.DamageAvoided) || Type == OverlayType.HPS || Type == OverlayType.FocusDPS) && UsingLeaderboard;
        public GridLength LeaderboardRowHeight => HasLeaderboard ? new GridLength(20) : new GridLength(0);
        public GridLength TotalRowHeight => new GridLength(20);
        public double TotalFontSize => Math.Max(9, 12 * SizeScalar);
        public bool AddSecondaryToValue { get; set; } = false;
        public bool FlipSecondaryAndPrimaryBars { get; set; } = false;
        public event Action<OverlayInstanceViewModel> OverlayClosed = delegate { };
        public void OverlayClosing()
        {
            Dispose();
            OverlayClosed(this);
        }
        private void Dispose()
        {
            CombatLogStreamer.CombatStarted -= Reset;
            CombatSelectionMonitor.CombatSelected -= UpdateMetrics;
            CombatSelectionMonitor.PhaseSelected -= UpdateMetrics;
            Leaderboards.LeaderboardStandingsAvailable -= UpdateStandings;
            Leaderboards.TopLeaderboardEntriesAvailable -= UpdateTopEntries;
            Leaderboards.LeaderboardTypeChanged -= UpdateLeaderboardType;
            _updateSub.Dispose();
        }
        public OverlayInstanceViewModel(OverlayType type) : base(type.ToString())
        {
            SettingsType = OverlaySettingsType.Character;
            Dispatcher.UIThread.Invoke(() =>
            {
                MainContent = new InfoOverlay(this);
            });

            CloseRequested += OverlayClosing;
            CreatedType = type;
            Type = type;
            if (type == OverlayType.EHPS)
            {
                SecondaryType = OverlayType.ProvidedAbsorb;
                AddSecondaryToValue = true;
            }
            if (type == OverlayType.HPS)
            {
                SecondaryType = OverlayType.ProvidedAbsorb;
                AddSecondaryToValue = true;
            }
            if (type == OverlayType.DPS)
            {
                SecondaryType = OverlayType.FocusDPS;
                AddSecondaryToValue = true;
            }
            if (type == OverlayType.DamageTaken)
            {
                SecondaryType = OverlayType.Mitigation;
                FlipSecondaryAndPrimaryBars = true;
            }
            if (type == OverlayType.Mitigation)
            {
                Type = OverlayType.ShieldAbsorb;
                SecondaryType = OverlayType.DamageAvoided;
                AddSecondaryToValue = true;
            }
            if (type == OverlayType.ShieldAbsorb)
            {
                SecondaryType = OverlayType.DamageAvoided;
                AddSecondaryToValue = true;
            }
            CombatLogStreamer.CombatStarted += Reset;
            CombatSelectionMonitor.OnInProgressCombatSelected += UpdateMetrics;
            _updateSub = Observable.FromEvent<Combat>(manager => CombatSelectionMonitor.CombatSelected += manager,
    manager => CombatSelectionMonitor.CombatSelected -= manager).Subscribe(DisplayFinalMetrics);
            CombatSelectionMonitor.PhaseSelected += UpdateMetrics;
            Leaderboards.LeaderboardStandingsAvailable += UpdateStandings;
            Leaderboards.TopLeaderboardEntriesAvailable += UpdateTopEntries;
            Leaderboards.LeaderboardTypeChanged += UpdateLeaderboardType;
            MetricColorLoader.OnOverlayTypeColorUpdated += UpdateColor;
            UpdateLeaderboardType(LeaderboardSettings.ReadLeaderboardSettings());
            this.WhenAnyValue(x => x.SizeScalar).Subscribe(_ => this.RaisePropertyChanged(nameof(TotalFontSize)));
        }


        private void UpdateColor(OverlayType type)
        {
            if (type == Type)
                this.RaisePropertyChanged(nameof(Type));
            if(type == SecondaryType)
                this.RaisePropertyChanged(nameof(SecondaryType));
        }
        private IDisposable _updateSub;
        private bool _usingLeaderboard;
        private Bitmap _overlayTypeImage = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/SwtorLogo_opaque.png")));
        private ObservableCollection<OverlayMetricInfo> _metricBars = new ObservableCollection<OverlayMetricInfo>();
        private OverlayType _type;

        private void UpdateLeaderboardType(LeaderboardType obj)
        {
            if (obj == LeaderboardType.Off)
            {
                UsingLeaderboard = false;
            }
            else
            {
                UsingLeaderboard = true;
            }
            if (obj == LeaderboardType.AllDiciplines)
                OverlayTypeImage = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/SwtorLogo_opaque.png")));
            else
            {
                OverlayTypeImage = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/LocalPlayerIcon.png")));
            }
        }

        private void UpdateTopEntries(Dictionary<LeaderboardEntryType, (string, double)> obj)
        {
            if (obj.Count == 0)
            {
                OrderMetricBars();
                return;
            }
            UpdateLeaderboardValues(obj);
        }

        private void UpdateStandings(Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>> obj)
        {
            AddLeaderboardStandings(obj);
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

        internal void RoleChanged(string role)
        {
            SetRole(role);
        }

        public void Reset()
        {
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
            OverlaysMoveable = false;
        }
        public void UnlockOverlays()
        {
            OverlaysMoveable = true;
        }
        private void AddLeaderboardBar(string characterName, double value)
        {
            if (_metricBarsDict.Any(mb => mb.Key.Item2 && mb.Value.Value == value))
                return;
            if (_metricBarsDict.Any(mb => mb.Key.Item2 && mb.Value.Value != value))
            {
                var staleTop = _metricBarsDict.FirstOrDefault(mb => mb.Key.Item2 && mb.Value.Value != value);
                _metricBarsDict.TryRemove(staleTop);
            }
            var metricbar = new OverlayMetricInfo
            {
                Type = Type,
                Player = new Entity { Name = characterName },
                Value = value,
                IsLeaderboardValue = true,
                RelativeLength = 1,
                SizeScalar = SizeScalar,
                MedalIconPath = new Bitmap(AssetLoader.Open(new Uri("avares://Orbs/resources/crownIcon.png")))
            };
            _metricBarsDict.TryAdd((characterName, true), metricbar);
            OrderMetricBars();
        }
        private void AddLeaderboardStanding(OverlayMetricInfo metricToUpdate, ConcurrentDictionary<LeaderboardEntryType, (double, bool)> standings)
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
        private void DisplayFinalMetrics(Combat combat)
        {
            ResetMetrics();
            UpdateMetrics(combat);
        }
        private void UpdateMetrics(Combat combat)
        {

            RefreshBarViews(combat);
            double sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value);
            if (CreatedType == OverlayType.DPS || CreatedType == OverlayType.EHPS || CreatedType == OverlayType.Mitigation)
                sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value + b.Value.SecondaryValue);
            if (CreatedType == OverlayType.DamageTaken)
                sum = _metricBarsDict.Where(b => !b.Key.Item2).Sum(b => b.Value.Value);

            MetricTotal = sum.ToString("N0");
        }
        private void RefreshBarViews(Combat combatToDisplay)
        {
            OverlayMetricInfo metricToUpdate;
            
            if (combatToDisplay.CharacterParticipants.Count == 0)
                return;
            foreach (var participant in combatToDisplay.CharacterParticipants)
            {
                var swtorClass =
                    CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(participant, combatToDisplay.StartTime);
                if (_metricBarsDict.Where(b => !b.Key.Item2).Any(m => m.Key.Item1 == participant.Name))
                {
                    metricToUpdate = _metricBarsDict.FirstOrDefault(mb => mb.Key.Item1 == participant.Name && !mb.Key.Item2).Value;
                    if (metricToUpdate == null)
                        continue;
                }
                else
                {
                    metricToUpdate = new OverlayMetricInfo() { ClassIcon =  IconFactory.GetClassIcon(swtorClass.Discipline),Player = participant, Type = Type, AddSecondayToValue = AddSecondaryToValue, FlipSecondaryAndPrimaryBars = FlipSecondaryAndPrimaryBars, SizeScalar = SizeScalar };
                    _metricBarsDict.TryAdd((participant.Name, false), metricToUpdate);
                }
                var primaryType = Type;
                var secondaryType = SecondaryType;

                UpdateMetric(primaryType, metricToUpdate, combatToDisplay, participant);
                if (SecondaryType != OverlayType.None)
                {
                    UpdateSecondary(secondaryType, metricToUpdate, combatToDisplay, participant);
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
                var maxValue = _metricBarsDict.MaxBy(m => m.Value.OrderingValue).Value.OrderingValue;
                foreach (var metric in _metricBarsDict)
                {
                    if (metric.Value.OrderingValue == 0 || double.IsInfinity(metric.Value.OrderingValue) || double.IsNaN(metric.Value.OrderingValue))
                        metric.Value.RelativeLength = 0;
                    else
                        metric.Value.RelativeLength = maxValue == 0 ? 0 : (metric.Value.OrderingValue / maxValue);
                }

                var listOfBars = new List<OverlayMetricInfo>();
                var keys = _metricBarsDict.Keys.ToList();
                for (var i = 0; i < keys.Count; i++)
                {
                    OverlayMetricInfo bar;
                    var worked = _metricBarsDict.TryGetValue(keys[i], out bar);
                    if (worked)
                        listOfBars.Add(bar);
                }

                MetricBars = new ObservableCollection<OverlayMetricInfo>(listOfBars.OrderByDescending(mb => mb.TrueValue));
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to order overlay metrics: " + ex.Message);
            }

        }
        private void AddLeaderboardStandings(Dictionary<Entity, ConcurrentDictionary<LeaderboardEntryType, (double, bool)>> leaderboardInfo)
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
            _metricBarsDict = new ConcurrentDictionary<(string, bool), OverlayMetricInfo>();
        }
        private void UpdateSecondary(OverlayType type, OverlayMetricInfo metric, Combat combat, Entity participant)
        {
            double value = MetricGetter.GetValueForMetric(type, combat, participant);
            metric.SecondaryType = type;
            metric.SecondaryValue = value;
        }
        private void UpdateMetric(OverlayType type, OverlayMetricInfo metricToUpdate, Combat obj, Entity participant)
        {
            var originalType = type;
            if (type == OverlayType.DPS)
                type = OverlayType.FluffDPS;
            if(type == OverlayType.EHPS)
                type = OverlayType.EHPSNoShielding;
            var value = MetricGetter.GetValueForMetric(type, obj, participant);
            metricToUpdate.Value = value;
            metricToUpdate.Type = originalType;
        }
    }
}
