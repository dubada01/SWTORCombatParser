using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public class CombatInfoInstance
    {
        public string SortItem { get; set; }
        public int Rate { get; set; }
        public int Total { get; set; }
        public int Average { get; set; }
        public int Count { get; set; }
        public double CritPercent { get; set; }
        public int Max { get; set; }
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
        BySource,
        ByAbility,
        ByTarget
    }
    public class TableInstanceViewModel :OverviewInstanceViewModel, INotifyPropertyChanged
    {
        private SortingOption sortingOption;
        public override SortingOption SortingOption { get => sortingOption; set
            { 
                sortingOption = value;
                Update();
            } 
        }
        public List<CombatInfoInstance> DataToView { get; set; }

        public TableInstanceViewModel(OverviewDataType type) : base(type)
        {

        }
        public override void UpdateData(Combat combat)
        {
            SelectedCombat = combat;
            Update();
        }
        public override void RemoveData(Combat combat)
        {
            SelectedCombat = null;
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
            OnPropertyChanged("DataToView");
        }
        private void DisplayDamageTakenData(Combat combat)
        {

            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.IncomingDamageLogs[_selectedEntity]);
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void DisplayHealingData(Combat combat)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.OutgoingHealingLogs[_selectedEntity]);
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void DisplayDamageData(Combat combat)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.OutgoingDamageLogs[_selectedEntity]);
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }
        private void DisplayHealingReceived(Combat combat)
        {

            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.IncomingHealingLogs[_selectedEntity]);
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
                Total = (int)orderedKey.Value.Sum(v => v.Value.EffectiveDblValue),
                Rate = (int)(orderedKey.Value.Sum(v => v.Value.EffectiveDblValue)/SelectedCombat.DurationSeconds),
                Average = (int)orderedKey.Value.Average(v => v.Value.EffectiveDblValue),
                Max = (int)orderedKey.Value.Max(v => v.Value.EffectiveDblValue),
                Count = (int)orderedKey.Value.Count(),
                CritPercent = orderedKey.Value.Count(v=>v.Value.WasCrit) / (double)orderedKey.Value.Count() * 100d,
            });
        }

        private Dictionary<string, List<ParsedLogEntry>> GetDataSplitOut(Combat combat, List<ParsedLogEntry> logsInScope)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = new Dictionary<string, List<ParsedLogEntry>>();
            switch (SortingOption)
            {
                case SortingOption.ByAbility:
                    splitOutdata = combat.GetByAbility(logsInScope).ToDictionary(kvp=>kvp.Key,kvp=>kvp.Value);
                    break;
                case SortingOption.BySource:
                    splitOutdata = combat.GetBySource(logsInScope).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
                case SortingOption.ByTarget:
                    splitOutdata = combat.GetByTarget(logsInScope).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
            }
            return splitOutdata;
        }
    }
}
