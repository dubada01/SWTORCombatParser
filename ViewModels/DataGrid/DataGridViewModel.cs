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
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.DataGrid
{
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

            // Extract the values to compare
            if (!double.TryParse(x.StatsSlots.FirstOrDefault(s => s.Header == _sortProperty)?.Value, out double xValue))
                xValue = 0; // or handle as needed
            if (!double.TryParse(y.StatsSlots.FirstOrDefault(s => s.Header == _sortProperty)?.Value, out double yValue))
                yValue = 0; // or handle as needed

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
        private List<OverlayType> _selectedColumnTypes = _defaultColumns;
        private static List<OverlayType> _defaultColumns = new() { OverlayType.DPS, OverlayType.Damage, OverlayType.EHPS, OverlayType.EffectiveHealing, OverlayType.DamageTaken, OverlayType.APM };
        private ObservableCollection<MemberInfoViewModel> partyMembers = new();
        private ObservableCollection<DataGridHeaderViewModel> headerNames;
        private string _localPlayer = "";
        private string _selectedNewColumn;

        public DataGridViewModel()
        {
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
            _localPlayer = discipline.Role.ToString();
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

        public List<string> GetCurrentColumnNames()
        {
            return _selectedColumnTypes.Select(GetNameFromType).ToList();
        }
        private void RefreshColumns()
        {
            if (!string.IsNullOrEmpty(_localPlayer))
                _selectedColumnTypes = DataGridDefaults.GetDefaults(_localPlayer);
            else
                _selectedColumnTypes = _defaultColumns;
        }

        private void UpdateUI()
        {
            if (_currentCombat is null)
                return;

            var orderedColumns = _columnOrder
                .Where(_selectedColumnTypes.Contains)
                .ToList();

            // *** deterministic participant order (keeps DataGrid rows stable) ***
            var participantsSnapshot = _currentCombat.CharacterParticipants
                .DistinctBy(p => p.LogId) // avoid duplicates
                .OrderBy(p => p.Name) // …or .OrderByDescending(p => p.TotalDps)
                .ToList();

            Dispatcher.UIThread.Invoke(() =>
            {
                // --- 1. pull any totals row off the list -------------------------
                var hadTotalsRow = PartyMembers.LastOrDefault()?.IsTotalsRow == true;
                if (hadTotalsRow)
                    PartyMembers.RemoveAt(PartyMembers.Count - 1);

                // --- 2. quick lookup of existing VMs by LogId --------------------
                var vmById = PartyMembers
                    .Where(vm => vm._entity != null)
                    .ToDictionary(vm => vm._entity!.LogId);

                // --- 3. create / refresh / reorder -------------------------------
                var newList = new List<MemberInfoViewModel>();

                for (int i = 0; i < participantsSnapshot.Count; i++)
                {
                    var p = participantsSnapshot[i];

                    if (vmById.TryGetValue(p.LogId, out var existing))
                    {
                        existing.Update(_currentCombat, orderedColumns);
                        vmById.Remove(p.LogId); // mark as consumed
                        newList.Add(existing);
                    }
                    else
                    {
                        newList.Add(new MemberInfoViewModel(i, p, _currentCombat, orderedColumns));
                    }
                }

                // --- 4. dispose VMs for players that disappeared -----------------
                foreach (var orphan in vmById.Values)
                    PartyMembers.Remove(orphan);

                // --- 5. update list order in-place to avoid CollectionChanged churn
                for (int i = 0; i < newList.Count; i++)
                {
                    if (i >= PartyMembers.Count)
                        PartyMembers.Add(newList[i]);
                    else if (!ReferenceEquals(PartyMembers[i], newList[i]))
                        PartyMembers[i] = newList[i];
                }

                while (PartyMembers.Count > newList.Count)
                    PartyMembers.RemoveAt(PartyMembers.Count - 1);

                // --- 6. append (or rebuild) the totals row -----------------------
                if (hadTotalsRow || PartyMembers.All(vm => vm._entity != null))
                {
                    PartyMembers.Add(new MemberInfoViewModel(
                        PartyMembers.Count, null, _currentCombat, orderedColumns)
                    {
                        IsTotalsRow = true
                    });
                }
            });
            ColumnsRefreshed();
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
            _selectedColumnTypes.Add(_columnOrder.FirstOrDefault(c => GetNameFromType(c) == obj));
            DataGridDefaults.SetDefaults(_selectedColumnTypes, _localPlayer);
            UpdateUI();
            this.RaisePropertyChanged(nameof(CanAddColumns));
            this.RaisePropertyChanged(nameof(AvailableColumns));
        }

        public void RemoveHeader(OverlayType obj)
        {
            var removedHeader = _selectedColumnTypes.FirstOrDefault(c => c == obj);
            _selectedColumnTypes.Remove(removedHeader);
            DataGridDefaults.SetDefaults(_selectedColumnTypes, _localPlayer);
            UpdateUI();
            this.RaisePropertyChanged(nameof(CanAddColumns));
            this.RaisePropertyChanged(nameof(AvailableColumns));
        }
        private string GetNameFromType(OverlayType type)
        {
            return (string)new OverlayTypeToReadableNameConverter().Convert(type, null, null, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
