using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public class CombatInfoInstance
    {
        public SolidColorBrush RowBackground { get; set; }
        public double SumTotal { get; set; }
        public string SortItem { get; set; }
        public double Rate => Math.Round(RateDouble);
        public double RateDouble { get; set; }
        public double Total { get; set; }
        public double PercentOfTotal => (Total / SumTotal)*100;
        public string PercentOfTotalStr => Math.Round((PercentOfTotal * 100)).ToString() + "%";
        public double Average { get; set; }
        public int Count { get; set; }
        public double CritPercent { get; set; }
        public double MaxCrit { get; set; }
        public double Max { get; set; }
        public string Type { get; set; }
        public Bitmap Icon { get; set; }
    }
    public enum OverviewDataType
    {
        Damage,
        Healing,
        DamageTaken,
        HealingReceived,
        Threat
    }
    public enum SortingOption
    {
        ByAbility,
        BySource,
        ByTarget
    }
    public class TableInstanceViewModel : OverviewInstanceViewModel
    {
        private SortingOption sortingOption;
        private double _sumTotal = 0;
        private ObservableCollection<CombatInfoInstance> _dataToView;
        private ObservableCollection<CombatInfoInstance> _totals;

        public override SortingOption SortingOption
        {
            get => sortingOption; set
            {
                this.RaiseAndSetIfChanged(ref sortingOption, value);
                Update();
            }
        }
        public string SelectedSortName => GetSortNameFromEnum(SortingOption);

        public ObservableCollection<CombatInfoInstance> DataToView
        {
            get => _dataToView;
            set => this.RaiseAndSetIfChanged(ref _dataToView, value);
        }

        public ObservableCollection<CombatInfoInstance> Totals
        {
            get => _totals;
            set => this.RaiseAndSetIfChanged(ref _totals, value);
        }

        public TableInstanceViewModel(OverviewDataType type) : base(type)
        {

        }
        public override void UpdateData(Combat combat)
        {
            SelectedCombat = combat;
            Update();
        }
        public override void Reset()
        {
            _selectedEntity = null;
            SelectedCombat = null;
            DataToView = new ObservableCollection<CombatInfoInstance>();
            Totals = new ObservableCollection<CombatInfoInstance>();
        }
        internal override void UpdateParticipant()
        {
            Update();
        }
        internal override async void Update()
        {
            if (_selectedEntity == null || SelectedCombat == null)
                return;
            DataToView = new ObservableCollection<CombatInfoInstance>();
            var list = new List<CombatInfoInstance>();
            switch (_type)
            {
                case OverviewDataType.Damage:
                    await DisplayDamageData(SelectedCombat, list);
                    break;
                case OverviewDataType.Healing:
                    await DisplayHealingData(SelectedCombat, list);
                    break;
                case OverviewDataType.DamageTaken:
                    await DisplayDamageTakenData(SelectedCombat, list);
                    break;
                case OverviewDataType.HealingReceived:
                    await DisplayHealingReceived(SelectedCombat, list);
                    break;
                case OverviewDataType.Threat:
                    await DisplayThreat(SelectedCombat, list);
                    break;
            }
            list = list.OrderByDescending(v => v.PercentOfTotal).ToList();
            if (list.Any())
            {
                Totals = new ObservableCollection<CombatInfoInstance>();
                Totals.Add(new CombatInfoInstance
                {

                    SumTotal = _sumTotal,
                    Total = list.Sum(v => v.Total),
                    RateDouble = list.Sum(v => v.RateDouble),
                    Average = list.Average(v => v.Average),
                    Max = list.Max(v => v.Max),
                    MaxCrit = list.Max(v => v.MaxCrit),
                    Count = list.Sum(v => v.Count),
                    CritPercent = list.Average(v => v.CritPercent)
                });
            }
            Dispatcher.UIThread.Invoke(() => {
                DataToView = new ObservableCollection<CombatInfoInstance>(list);
            });
            
        }
        private string GetSortNameFromEnum(SortingOption enumValue)
        {
            switch (enumValue)
            {
                case SortingOption.ByAbility:
                    return "Ability Name";
                case SortingOption.BySource:
                    return "Source Name";
                case SortingOption.ByTarget:
                    return "Target Name";
                default:
                    return "Unknown";
            }
        }
        private async Task DisplayDamageTakenData(Combat combat, List<CombatInfoInstance> list)
        {
            // Filter entities with matching LogId
            var selectedLogId = _selectedEntity.LogId;
            var matchingEntities = combat.OutgoingDamageLogs
                .Where(kvp => kvp.Key.LogId == selectedLogId)
                .Select(kvp => kvp.Key);

            // Collect all relevant logs for matching entities
            var combinedLogs = matchingEntities
                .SelectMany(entity => combat.IncomingDamageLogs.ContainsKey(entity)
                    ? combat.IncomingDamageLogs[entity]
                    : new ConcurrentQueue<ParsedLogEntry>());

            // Group data by entity name and average values
            var splitOutData = GetDataSplitOut(combat, combinedLogs);

            // Update _sumTotal with the average
            _sumTotal = splitOutData.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));

            // Populate rows asynchronously
            foreach (var orderedKey in splitOutData)
            {
                await PoppulateRows(orderedKey, list);
            }
        }

        private async Task DisplayHealingData(Combat combat, List<CombatInfoInstance> list)
        {
            var selectedLogId = _selectedEntity.LogId;
            var matchingEntities = combat.OutgoingHealingLogs
                .Where(kvp => kvp.Key.LogId == selectedLogId)
                .Select(kvp => kvp.Key);

            var healing = matchingEntities
                .SelectMany(entity => combat.OutgoingHealingLogs.ContainsKey(entity) ? combat.OutgoingHealingLogs[entity] : new ConcurrentQueue<ParsedLogEntry>());

            var shielding = matchingEntities
                .SelectMany(entity => combat.ShieldingProvidedLogs.ContainsKey(entity) ? combat.ShieldingProvidedLogs[entity] : new ConcurrentQueue<ParsedLogEntry>());

            var both = healing.Concat(shielding);
            var splitOutdata = GetDataSplitOut(combat, both.ToList());
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                await PoppulateRows(orderedKey, list);
            }
        }

      private async Task DisplayDamageData(Combat combat, List<CombatInfoInstance> list)
{
    var selectedLogId = _selectedEntity.LogId;
    var matchingEntities = combat.OutgoingDamageLogs
        .Where(kvp => kvp.Key.LogId == selectedLogId)
        .Select(kvp => kvp.Key);

    var combinedLogs = matchingEntities
        .SelectMany(entity => combat.OutgoingDamageLogs.ContainsKey(entity) ? combat.OutgoingDamageLogs[entity] : new ConcurrentQueue<ParsedLogEntry>());

    var splitOutdata = GetDataSplitOut(combat, combinedLogs.ToList());
    _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
    foreach (var orderedKey in splitOutdata)
    {
        await PoppulateRows(orderedKey, list);
    }
}

private async Task DisplayHealingReceived(Combat combat, List<CombatInfoInstance> list)
{
    var selectedLogId = _selectedEntity.LogId;
    var matchingEntities = combat.IncomingHealingLogs
        .Where(kvp => kvp.Key.LogId == selectedLogId)
        .Select(kvp => kvp.Key);

    var combinedLogs = matchingEntities
        .SelectMany(entity => combat.IncomingHealingLogs.ContainsKey(entity) ? combat.IncomingHealingLogs[entity] : new ConcurrentQueue<ParsedLogEntry>());

    var splitOutdata = GetDataSplitOut(combat, combinedLogs.ToList());
    _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
    foreach (var orderedKey in splitOutdata)
    {
        await PoppulateRows(orderedKey, list);
    }
}

private async Task DisplayThreat(Combat combat, List<CombatInfoInstance> list)
{
    var selectedLogId = _selectedEntity.LogId;
    var matchingEntities = combat.LogsInvolvingEntity
        .Where(kvp => kvp.Key == selectedLogId)
        .Select(kvp => kvp.Key);

    var combinedLogs = matchingEntities
        .SelectMany(entity => combat.LogsInvolvingEntity.TryGetValue(entity, out var value)
            ? value.Where(l => l.Source.LogId == entity && l.Threat != 0)
            : new List<ParsedLogEntry>());

    var splitOutdata = GetDataSplitOut(combat, combinedLogs.ToList());
    _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Where(v => v.Threat >= 0).Sum(v => v.Threat));
    foreach (var orderedKey in splitOutdata)
    {
        await PoppulateRowsThreat(orderedKey, list);
    }
}
        private async Task PoppulateRowsThreat(KeyValuePair<string, ConcurrentQueue<ParsedLogEntry>> orderedKey, List<CombatInfoInstance> list)
        {
            list.Add(new CombatInfoInstance
            {
                SortItem =string.IsNullOrEmpty(orderedKey.Key) ? "Taunt":orderedKey.Key,
                SumTotal = _sumTotal,
                Total = (int)orderedKey.Value.Sum(v => v.Threat),
                RateDouble = orderedKey.Value.Sum(v => v.Threat) / SelectedCombat.DurationSeconds,
                Average = (int)orderedKey.Value.Average(v => v.Threat),
                Max = orderedKey.Value.Any(a => !a.Value.WasCrit) ? (int)orderedKey.Value.Where(v => !v.Value.WasCrit).Max(v => v.Threat) : 0,
                MaxCrit = orderedKey.Value.Any(a => a.Value.WasCrit) ? (int)orderedKey.Value.Where(v => v.Value.WasCrit).Max(v => v.Threat) : 0,
                Count = (int)orderedKey.Value.Count(),
                CritPercent = orderedKey.Value.Count(v => v.Value.WasCrit) / (double)orderedKey.Value.Count() * 100d,
                Icon = await GetIconForRow(orderedKey.Value.FirstOrDefault())
            });
        }
        private async Task PoppulateRows(KeyValuePair<string, ConcurrentQueue<ParsedLogEntry>> orderedKey, List<CombatInfoInstance> list)
        {
            list.Add(new CombatInfoInstance
            {
                SortItem = orderedKey.Key,
                SumTotal = _sumTotal,
                Total = (int)orderedKey.Value.Sum(v => v.Value.EffectiveDblValue),
                RateDouble = orderedKey.Value.Sum(v => v.Value.EffectiveDblValue) / SelectedCombat.DurationSeconds,
                Average = (int)orderedKey.Value.Average(v => v.Value.EffectiveDblValue),
                Max = orderedKey.Value.Any(a => !a.Value.WasCrit) ? (int)orderedKey.Value.Where(v => !v.Value.WasCrit).Max(v => v.Value.EffectiveDblValue) : 0,
                MaxCrit = orderedKey.Value.Any(a => a.Value.WasCrit) ? (int)orderedKey.Value.Where(v => v.Value.WasCrit).Max(v => v.Value.EffectiveDblValue) : 0,
                Count = (int)orderedKey.Value.Count(),
                CritPercent = orderedKey.Value.Count(v => v.Value.WasCrit) / (double)orderedKey.Value.Count() * 100d,
                Icon = await GetIconForRow(orderedKey.Value.FirstOrDefault())
            });
        }
        private async Task<Bitmap> GetIconForRow(ParsedLogEntry log)
        {
            if(log == null) return null;
            var sourceClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(log.Source, log.TimeStamp);
            var targetClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(log.Target, log.TimeStamp);
            switch (sortingOption)
            {
                case SortingOption.ByAbility:
                    return await IconGetter.GetIconPathForLog(log);
                case SortingOption.BySource:
                    return IconFactory.GetClassIcon(sourceClass.Discipline);
                case SortingOption.ByTarget:
                    return IconFactory.GetClassIcon(targetClass.Discipline);
                default:
                    return null;
            }
        }
        private Dictionary<string, ConcurrentQueue<ParsedLogEntry>> GetDataSplitOut(Combat combat, IEnumerable<ParsedLogEntry> logsInScope)
        {
            Dictionary<string, ConcurrentQueue<ParsedLogEntry>> splitOutdata = new Dictionary<string, ConcurrentQueue<ParsedLogEntry>>();
            switch (SortingOption)
            {
                case SortingOption.ByAbility:
                    splitOutdata = combat.GetByAbility(logsInScope).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
                case SortingOption.BySource:
                    splitOutdata = combat.GetBySourceName(logsInScope).ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
                    break;
                case SortingOption.ByTarget:
                    splitOutdata = combat.GetByTargetName(logsInScope).ToDictionary(kvp => kvp.Key.Name, kvp => kvp.Value);
                    break;
            }
            return splitOutdata;
        }
    }
}
