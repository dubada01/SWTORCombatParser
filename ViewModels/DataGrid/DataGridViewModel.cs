using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.DataGrid;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Utilities.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.DataGrid
{
    public class DataGridViewModel : INotifyPropertyChanged
    {
        private List<OverlayType> _columnOrder = new List<OverlayType> {
            OverlayType.DPS,OverlayType.NonEDPS,OverlayType.FocusDPS,OverlayType.BurstDPS,
            OverlayType.EHPS,OverlayType.HPS, OverlayType.BurstEHPS, OverlayType.HealReactionTime,
            OverlayType.DamageTaken, OverlayType.BurstDamageTaken, OverlayType.Mitigation, OverlayType.ShieldAbsorb, OverlayType.ProvidedAbsorb, OverlayType.DamageAvoided, OverlayType.ThreatPerSecond,OverlayType.DamageSavedDuringCD,
            OverlayType.InterruptCount, OverlayType.APM};
        private List<Combat> _allSelectedCombats = new List<Combat>();
        private List<OverlayType> _selectedColumnTypes = _defaultColumns;
        private static List<OverlayType> _defaultColumns = new List<OverlayType>() { OverlayType.DPS, OverlayType.BurstDPS, OverlayType.EHPS, OverlayType.BurstEHPS, OverlayType.DamageTaken, OverlayType.BurstDamageTaken, OverlayType.APM };
        private ObservableCollection<MemberInfoViewModel> partyMembers = new ObservableCollection<MemberInfoViewModel>();
        private ObservableCollection<DataGridHeaderViewModel> headerNames;
        private OverlayType _sortMetric;
        private SortingDirection _sortDirection;
        private string _localPlayer = "";

        public DataGridViewModel()
        {
            IconFactory.Init();
            DataGridDefaults.Init();
            CombatLogStateBuilder.PlayerDiciplineChanged += UpdateColumns;
            CombatLogStreamer.HistoricalLogsFinished += UpdateLocalPlayer;
            UpdateHeaders();
        }

        private void UpdateLocalPlayer(DateTime combatEndTime, bool localPlayerIdentified)
        {
            if (!localPlayerIdentified)
                return;
            var player = CombatLogStateBuilder.CurrentState.LocalPlayer;
            var discipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(combatEndTime);
            if (player == null || discipline == null)
                return;
            _localPlayer = player.Name + "_" + discipline.Discipline;
            RefreshColumns();
        }

        private void UpdateColumns(Entity arg1, SWTORClass arg2)
        {
            if (arg1 == null || arg2 == null)
                return;
            _localPlayer = arg1.Name + "_" + arg2.Discipline;
            RefreshColumns();
        }

        public ObservableCollection<DataGridHeaderViewModel> HeaderNames
        {
            get => headerNames; set
            {
                headerNames = value;
                OnPropertyChanged();
            }
        }
        public List<string> AvailableColumns => _columnOrder.Select(c => GetNameFromType(c)).Where(c => !HeaderNames.Any(h=>h.Text == c)).ToList();
        public ObservableCollection<MemberInfoViewModel> PartyMembers
        {
            get => partyMembers; set
            {
                partyMembers = value;
                OnPropertyChanged();
            }
        }
        public void UpdateCombat(Combat updatedCombat)
        {
            _allSelectedCombats.Clear();
            _allSelectedCombats.Add(updatedCombat);
            
            UpdateUI();
        }
        public void AddCombat(Combat combatInfo)
        {
            _allSelectedCombats.Clear();
            _allSelectedCombats.Add(combatInfo);
            UpdateUI();
        }
        public void RemoveCombat(Combat combatInfo)
        {
            _allSelectedCombats.Remove(combatInfo);
            UpdateUI();
        }
        public void Reset()
        {
            _localPlayer = "";
            _allSelectedCombats.Clear();
            UpdateUI();
        }
        private void RefreshColumns()
        {
            if (!string.IsNullOrEmpty(_localPlayer))
                _selectedColumnTypes = DataGridDefaults.GetDefaults(_localPlayer);
            else
                _selectedColumnTypes = _defaultColumns;
        }
        private void UpdateHeaders()
        {
            var orderedSelectedColumns = _columnOrder.Where(o => _selectedColumnTypes.Contains(o)).ToList();
            HeaderNames = new ObservableCollection<DataGridHeaderViewModel>(orderedSelectedColumns.Select(c =>
            new DataGridHeaderViewModel() { Text = GetNameFromType(c), SortDirection = c == _sortMetric ? _sortDirection : SortingDirection.None }));
            HeaderNames.Insert(0, new DataGridHeaderViewModel() { Text = "Name", IsName = true });
            if (orderedSelectedColumns.Count < 10)
                HeaderNames.Add(new DataGridHeaderViewModel { AvailableHeaderNames = AvailableColumns, IsRealHeader = false });
            foreach (var headerName in HeaderNames)
            {
                headerName.RequestRemoveHeader += RemoveHeader;
                headerName.RequestedNewHeader += AddHeader;
                headerName.SortingDirectionChanged += Sort;
            }
        }
        private void UpdateUI()
        {
            UpdateHeaders();
            var orderedSelectedColumns = _columnOrder.Where(o => _selectedColumnTypes.Contains(o)).ToList();
            var newPlayers = _allSelectedCombats.SelectMany(c => c.CharacterParticipants).Distinct().Select((pm, i) => new MemberInfoViewModel(i, pm, _allSelectedCombats, orderedSelectedColumns));
            PartyMembers.Clear();
            var sortedMembers = new List<MemberInfoViewModel>();
            if (_sortDirection == SortingDirection.Ascending)
            {
                sortedMembers = newPlayers.OrderBy(v => GetValueForMetric(_sortMetric, _allSelectedCombats, v._entity)).ToList();
            }
            if (_sortDirection == SortingDirection.Descending)
            {
                sortedMembers = newPlayers.OrderByDescending(v => GetValueForMetric(_sortMetric, _allSelectedCombats, v._entity)).ToList();
            }
            if(_sortDirection == SortingDirection.None)
            {
                sortedMembers = newPlayers.ToList();
            }
            for(var i = 0; i < sortedMembers.Count; i++)
            {
                sortedMembers[i].AssignBackground(i);
            }
            foreach(var member in sortedMembers)
            {
                PartyMembers.Add(member);
            }
        }

        private void Sort(SortingDirection arg1, string arg2)
        {
            _sortMetric = _columnOrder.First(v => GetNameFromType(v) == arg2);
            _sortDirection = arg1;
            UpdateUI();
        }

        private void AddHeader(string obj)
        {
            _selectedColumnTypes.Add(_columnOrder.FirstOrDefault(c => GetNameFromType(c) == obj));
            DataGridDefaults.SetDefaults(_selectedColumnTypes,_localPlayer);
            UpdateUI();
        }

        private void RemoveHeader(DataGridHeaderViewModel obj)
        {
            var removedHeader = _selectedColumnTypes.FirstOrDefault(c => GetNameFromType(c) == obj.Text);
            _selectedColumnTypes.Remove(removedHeader);
            DataGridDefaults.SetDefaults(_selectedColumnTypes, _localPlayer);
            UpdateUI();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private string GetNameFromType(OverlayType type)
        {
            return (string)new OverlayTypeToReadableNameConverter().Convert(type, null, null, System.Globalization.CultureInfo.InvariantCulture);
        }
        private static double GetValueForMetric(OverlayType type, List<Combat> combats, Entity participant)
        {
            double value = 0;
            switch (type)
            {
                case OverlayType.APM:
                    value = combats.SelectMany(c => c.APM).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DPS:
                    value = combats.SelectMany(c => c.EDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.NonEDPS:
                    value = combats.SelectMany(c => c.DPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.EHPS:
                    value = combats.SelectMany(c => c.EHPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    value += combats.SelectMany(c => c.PSPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.ProvidedAbsorb:
                    value = combats.SelectMany(c => c.PSPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.FocusDPS:
                    value = combats.SelectMany(c => c.EFocusDPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.ThreatPerSecond:
                    value = combats.SelectMany(c => c.TPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.Threat:
                    value = combats.SelectMany(c => c.TotalThreat).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DamageTaken:
                    value = combats.SelectMany(c => c.EDTPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.Mitigation:
                    value = combats.SelectMany(c => c.MPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DamageSavedDuringCD:
                    value = combats.SelectMany(c => c.DamageSavedFromCDPerSecond).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.DamageAvoided:
                    value = combats.SelectMany(c => c.DAPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.ShieldAbsorb:
                    value = combats.SelectMany(c => c.SAPS).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.BurstDPS:
                    value = combats.SelectMany(c => c.MaxBurstDamage).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.BurstEHPS:
                    value = combats.SelectMany(c => c.MaxBurstHeal).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.BurstDamageTaken:
                    value = combats.SelectMany(c => c.MaxBurstDamageTaken).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.HealReactionTime:
                    value = combats.SelectMany(c => c.NumberOfHighSpeedReactions).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.TankHealReactionTime:
                    value = combats.SelectMany(c => c.AverageTankDamageRecoveryTimeTotal).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
                case OverlayType.InterruptCount:
                    value = combats.SelectMany(c => c.TotalInterrupts).Where(v => v.Key == participant).Select(v => v.Value).Average();
                    break;
            }
            return value;
        }
    }
}
