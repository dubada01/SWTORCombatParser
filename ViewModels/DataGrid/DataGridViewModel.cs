using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.DataGrid;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public sealed class MetricColumnOption(DataGridViewModel owner, OverlayType type, string name) : ReactiveObject
    {
        public OverlayType Type { get; } = type;
        public string Name { get; } = name;
        public bool IsSelected { get => owner.HasMetric(Type); set => owner.SetMetric(Type, value); }
        public bool IsEnabled => IsSelected || owner.MetricCount < 8;
        internal void Refresh() { this.RaisePropertyChanged(nameof(IsSelected)); this.RaisePropertyChanged(nameof(IsEnabled)); }
    }
    public sealed record MetricColumnGroup(string Name, IReadOnlyList<MetricColumnOption> Metrics);
    public class CustomComparer : IComparer<MemberInfoViewModel>, IComparer
    {
        private readonly string _sortProperty;

        // Public property to allow dynamic updates of sort direction
        public ListSortDirection Direction { get; set; }

        public CustomComparer(string sortProperty, ListSortDirection direction)
        {
            _sortProperty = sortProperty;
            Direction = direction;
        }

        public int Compare(MemberInfoViewModel x, MemberInfoViewModel y)
        {
            // Handle nulls if necessary
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            // Ensure "Totals" row is always at the bottom
            if (x.IsTotalsRow && y.IsTotalsRow)
                return 0;
            if (x.IsTotalsRow)
                return 1;
            if (y.IsTotalsRow)
                return -1;

            if (_sortProperty == "Name")
            {
                var byName = StringComparer.CurrentCultureIgnoreCase.Compare(x.PlayerName, y.PlayerName);
                return Direction == ListSortDirection.Ascending ? byName : -byName;
            }

            var xValue = x.StatsSlots.FirstOrDefault(s => s.Header == _sortProperty)?.NumericValue ?? 0;
            var yValue = y.StatsSlots.FirstOrDefault(s => s.Header == _sortProperty)?.NumericValue ?? 0;

            // Compare the values
            int comparisonResult = Comparer<double>.Default.Compare(xValue, yValue);

            // Adjust the comparison result based on the current sort direction
            return Direction == ListSortDirection.Ascending ? comparisonResult : -comparisonResult;
        }

        public int Compare(object? x, object? y)
        {
            return Compare(x as MemberInfoViewModel, y as MemberInfoViewModel);
        }
    }

    public class DataGridViewModel : ReactiveObject
    {
        private List<OverlayType> _columnOrder = new()
        {
            OverlayType.DPS,OverlayType.Damage,OverlayType.SingleTargetDPS,OverlayType.NonEDPS,OverlayType.RawDamage,OverlayType.FocusDPS,OverlayType.BurstDPS,
            OverlayType.EHPS,OverlayType.SingleTargetEHPS,OverlayType.EffectiveHealing,OverlayType.HPS,OverlayType.RawHealing, OverlayType.BurstEHPS, OverlayType.HealReactionTime,OverlayType.CleanseCount,OverlayType.CleanseSpeed,
            OverlayType.DamageTaken, OverlayType.BurstDamageTaken, OverlayType.Mitigation, OverlayType.ShieldAbsorb, OverlayType.ProvidedAbsorb, OverlayType.DamageAvoided, OverlayType.ThreatPerSecond,OverlayType.DamageSavedDuringCD,
            OverlayType.InterruptCount, OverlayType.APM};

        private Combat? _currentCombat;
        private List<OverlayType> _selectedColumnTypes = new(_defaultColumns);
        private CustomComparer _sortComparer = new("Name", ListSortDirection.Ascending);
        private static List<OverlayType> _defaultColumns = new() { OverlayType.DPS, OverlayType.Damage, OverlayType.EHPS, OverlayType.EffectiveHealing, OverlayType.DamageTaken, OverlayType.APM };
        private ObservableCollection<MemberInfoViewModel> partyMembers = new();
        private ObservableCollection<DataGridHeaderViewModel> headerNames;
        private string _localPlayer = "";
        private string _selectedNewColumn;
        public IReadOnlyList<MetricColumnOption> MetricOptions { get; }
        public IReadOnlyList<MetricColumnGroup> MetricGroups { get; }
        public int MetricCount => _selectedColumnTypes.Count;
        public string MetricCountLabel => $"{MetricCount} / 8 metrics";
        public bool HasCombat => PartyMembers.Count > 0;
        public string CombatSummary => _currentCombat == null ? "" : $"{_currentCombat.CharacterParticipants.Count} players · {TimeSpan.FromMilliseconds(_currentCombat.DurationMS):mm\\:ss}";
        public ReactiveCommand<Unit, Unit> ResetMetricsCommand { get; }
        internal bool HasMetric(OverlayType type) => _selectedColumnTypes.Contains(type);
        internal void SetMetric(OverlayType type, bool enabled)
        {
            if (HasMetric(type) == enabled) return;
            if (enabled) AddHeader(GetNameFromType(type)); else RemoveHeader(type);
        }
        private void NotifyMetrics()
        {
            foreach (var option in MetricOptions) option.Refresh();
            this.RaisePropertyChanged(nameof(MetricCountLabel));
            this.RaisePropertyChanged(nameof(CanAddColumns));
            this.RaisePropertyChanged(nameof(AvailableColumns));
        }

        public DataGridViewModel()
        {
            MetricOptions = _columnOrder.Select(type => new MetricColumnOption(this, type, GetNameFromType(type))).ToArray();
            MetricGroups = new[] { new MetricColumnGroup("Damage", MetricOptions.Take(7).ToArray()),
                new MetricColumnGroup("Healing", MetricOptions.Skip(7).Take(9).ToArray()),
                new MetricColumnGroup("Defense", MetricOptions.Skip(16).Take(8).Where(m => m.Type != OverlayType.ThreatPerSecond).ToArray()),
                new MetricColumnGroup("Utility", MetricOptions.Where(m => m.Type is OverlayType.ThreatPerSecond or OverlayType.InterruptCount or OverlayType.APM).ToArray()) };
            ResetMetricsCommand = ReactiveCommand.Create(() =>
            {
                _selectedColumnTypes = new(_defaultColumns); DataGridDefaults.SetDefaults(_selectedColumnTypes, _localPlayer);
                UpdateUI(); NotifyMetrics();
            });
            IconFactory.Init();
            DataGridDefaults.Init();
            //CombatLogStateBuilder.PlayerDiciplineChanged += UpdateColumns;
            CombatLogStreamer.HistoricalLogsFinished += UpdateLocalPlayer;
        }


        private void UpdateLocalPlayer(DateTime combatEndTime, bool localPlayerIdentified)
        {
            if (!localPlayerIdentified)
                return;
            var player = CombatLogStateBuilder.CurrentState.LocalPlayer;
            var discipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(combatEndTime);
            if (player == null || discipline == null)
                return;
            var role = discipline.Role.ToString();
            if (_localPlayer == role) return;
            _localPlayer = role;
            RefreshColumns();
        }

        private void UpdateColumns(Entity arg1, SWTORClass arg2)
        {
            if (arg1 == null || arg2 == null)
                return;
            _localPlayer = arg1.Name + "_" + arg2.Discipline;
            RefreshColumns();
        }
        public event Action ColumnsRefreshed = delegate { };
        public event Action RowsReordering = delegate { };
        public event Action RowsReordered = delegate { };
        public bool CanAddColumns => _selectedColumnTypes.Count < 8 && PartyMembers.Count > 0;
        public ObservableCollection<MemberInfoViewModel> PartyMembers
        {
            get => partyMembers; set => this.RaiseAndSetIfChanged(ref partyMembers, value);
        }
        
        public void UpdateCombat(Combat updatedCombat)
        {
            _currentCombat = updatedCombat;
            UpdateLocalPlayer(updatedCombat.EndTime, true);
            this.RaisePropertyChanged(nameof(CanAddColumns));
            UpdateUI();
        }
        public void Reset()
        {
            _localPlayer = "";
            UpdateUI();
        }

        internal void ReleaseCombat()
        {
            _currentCombat = null;
            PartyMembers.Clear();
            this.RaisePropertyChanged(nameof(CanAddColumns));
            this.RaisePropertyChanged(nameof(HasCombat));
            this.RaisePropertyChanged(nameof(CombatSummary));
        }

        public List<string> GetCurrentColumnNames()
        {
            return _selectedColumnTypes.Select(GetNameFromType).ToList();
        }
        private void RefreshColumns()
        {
            if (!string.IsNullOrEmpty(_localPlayer))
                _selectedColumnTypes = DataGridDefaults.GetDefaults(_localPlayer);
            else
                _selectedColumnTypes = new(_defaultColumns);
            NotifyMetrics();
        }

        private void UpdateUI()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var combat = _currentCombat;
                if (combat == null) return;
                var columns = _columnOrder.Where(_selectedColumnTypes.Contains).ToList();
                var participants = combat.CharacterParticipants.Values.DistinctBy(p => p.LogId)
                    .OrderBy(p => p.Name).ToList();
                var participantIds = participants.Select(p => p.LogId).ToHashSet();

                // Keep surviving row objects (including totals) and publish only real
                // membership changes. A live tick must never clear the table.
                foreach (var row in PartyMembers.Where(row => !row.IsTotalsRow &&
                             !participantIds.Contains(row._entity.LogId)).ToList())
                    PartyMembers.Remove(row);
                var rowsById = PartyMembers.Where(row => !row.IsTotalsRow)
                    .ToDictionary(row => row._entity.LogId);
                foreach (var participant in participants)
                {
                    if (rowsById.TryGetValue(participant.LogId, out var row))
                        row.Update(combat, columns, participant);
                    else
                        PartyMembers.Add(new MemberInfoViewModel(PartyMembers.Count, participant, combat, columns));
                }
                var totals = PartyMembers.FirstOrDefault(row => row.IsTotalsRow);
                if (totals == null)
                    PartyMembers.Add(new MemberInfoViewModel(PartyMembers.Count, null, combat, columns));
                else
                    totals.Update(combat, columns);

                SortRows();
                this.RaisePropertyChanged(nameof(CanAddColumns));
                ColumnsRefreshed();
                this.RaisePropertyChanged(nameof(HasCombat));
                this.RaisePropertyChanged(nameof(CombatSummary));
            });
        }

        public void Sort(string property, ListSortDirection direction)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                _sortComparer = new CustomComparer(property, direction);
                SortRows();
            });
        }

        private void SortRows()
        {
            // OrderBy is stable, so tied values keep their current positions.
            var sorted = PartyMembers.OrderBy(row => row, _sortComparer).ToList();
            if (sorted.SequenceEqual(PartyMembers)) return;
            RowsReordering();
            try
            {
                for (var i = 0; i < sorted.Count; i++)
                {
                    var oldIndex = PartyMembers.IndexOf(sorted[i]);
                    if (oldIndex == i) continue;
                    // This DataGrid's collection view doesn't reflect Move events.
                    // Remove/insert only the displaced row using notifications it
                    // supports; keep the same row object and ItemsSource.
                    PartyMembers.RemoveAt(oldIndex);
                    PartyMembers.Insert(i, sorted[i]);
                }
            }
            finally { RowsReordered(); }
        }
        public List<string> AvailableColumns => _columnOrder.Select(GetNameFromType).Where(c => _selectedColumnTypes.All(h => GetNameFromType(h) != c)).ToList();
        public string SelectedNewColumn
        {
            get => _selectedNewColumn;
            set
            {
                _selectedNewColumn = value;
                if(!string.IsNullOrEmpty(_selectedNewColumn))
                    AddHeader(_selectedNewColumn);
                _selectedNewColumn = "";
                this.RaisePropertyChanged();
            }
        }
        
        public void AddHeader(string obj)
        {
            var option = MetricOptions.FirstOrDefault(c => c.Name == obj);
            if (option == null || HasMetric(option.Type) || MetricCount >= 8) return;
            _selectedColumnTypes.Add(option.Type);
            DataGridDefaults.SetDefaults(_selectedColumnTypes, _localPlayer);
            UpdateUI();
            this.RaisePropertyChanged(nameof(CanAddColumns));
            this.RaisePropertyChanged(nameof(AvailableColumns));
            NotifyMetrics();
        }

        public void RemoveHeader(OverlayType obj)
        {
            if (!HasMetric(obj)) return;
            var removedHeader = _selectedColumnTypes.FirstOrDefault(c => c == obj);
            _selectedColumnTypes.Remove(removedHeader);
            DataGridDefaults.SetDefaults(_selectedColumnTypes, _localPlayer);
            UpdateUI();
            this.RaisePropertyChanged(nameof(CanAddColumns));
            this.RaisePropertyChanged(nameof(AvailableColumns));
            NotifyMetrics();
        }
        private string GetNameFromType(OverlayType type)
        {
            return (string)new OverlayTypeToReadableNameConverter().Convert(type, null, null, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
