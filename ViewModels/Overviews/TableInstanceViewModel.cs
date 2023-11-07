using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Media;

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
        public double PercentOfTotal => Total / SumTotal;
        public string PercentOfTotalStr => Math.Round((PercentOfTotal * 100)).ToString() + "%";
        public double Average { get; set; }
        public int Count { get; set; }
        public double CritPercent { get; set; }
        public double MaxCrit { get; set; }
        public double Max { get; set; }
        public string Type { get; set; }
    }
    public enum OverviewDataType
    {
        Damage,
        Healing,
        DamageTaken,
        HealingReceived
    }
    public enum SortingOption
    {
        ByAbility,
        BySource,
        ByTarget
    }
    public class TableInstanceViewModel : OverviewInstanceViewModel, INotifyPropertyChanged
    {
        private SortingOption sortingOption;
        private double _sumTotal = 0;
        public override SortingOption SortingOption
        {
            get => sortingOption; set
            {
                sortingOption = value;
                OnPropertyChanged("SelectedSortName");
                Update();
            }
        }
        public string SelectedSortName => GetSortNameFromEnum(SortingOption);
        public List<CombatInfoInstance> DataToView { get; set; }

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
            DataToView = new List<CombatInfoInstance>();
            OnPropertyChanged("DataToView");
        }
        internal override void UpdateParticipant()
        {
            Update();
        }
        internal override void Update()
        {
            if (_selectedEntity == null || SelectedCombat == null)
                return;
            DataToView = new List<CombatInfoInstance>();
            switch (_type)
            {
                case OverviewDataType.Damage:
                    DisplayDamageData(SelectedCombat);
                    break;
                case OverviewDataType.Healing:
                    DisplayHealingData(SelectedCombat);
                    break;
                case OverviewDataType.DamageTaken:
                    DisplayDamageTakenData(SelectedCombat);
                    break;
                case OverviewDataType.HealingReceived:
                    DisplayHealingReceived(SelectedCombat);
                    break;
            }
            DataToView = DataToView.OrderByDescending(v => v.PercentOfTotal).ToList();
            if (DataToView.Any())
            {
                DataToView.Add(new CombatInfoInstance
                {
                    
                    SumTotal = _sumTotal,
                    Total = DataToView.Sum(v => v.Total),
                    RateDouble = DataToView.Sum(v => v.RateDouble),
                    Average = DataToView.Average(v => v.Average),
                    Max = DataToView.Max(v => v.Max),
                    MaxCrit = DataToView.Max(v => v.MaxCrit),
                    Count = DataToView.Sum(v => v.Count),
                    CritPercent = DataToView.Average(v => v.CritPercent)
                });
            }
            for (var i = 0; i < DataToView.Count; i++)
            {
                if (i % 2 == 1)
                {
                    DataToView[i].RowBackground = (SolidColorBrush)App.Current.FindResource("Gray4Brush");
                }
            }
            OnPropertyChanged("DataToView");
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
        private void DisplayDamageTakenData(Combat combat)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.IncomingDamageLogs[defaultEntity]);
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void DisplayHealingData(Combat combat)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            var healing = combat.OutgoingHealingLogs[defaultEntity];
            var shielding = combat.ShieldingProvidedLogs[defaultEntity];
            var both = healing.Concat(shielding);
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, both.ToList());
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void DisplayDamageData(Combat combat)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.OutgoingDamageLogs[defaultEntity]);
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }
        private void DisplayHealingReceived(Combat combat)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.IncomingHealingLogs[defaultEntity]);
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void PoppulateRows(KeyValuePair<string, List<ParsedLogEntry>> orderedKey)
        {
            DataToView.Add(new CombatInfoInstance
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
            });
        }

        private Dictionary<string, List<ParsedLogEntry>> GetDataSplitOut(Combat combat, List<ParsedLogEntry> logsInScope)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = new Dictionary<string, List<ParsedLogEntry>>();
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
