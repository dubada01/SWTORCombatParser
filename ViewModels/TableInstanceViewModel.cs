using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.ViewModels
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
    public enum TableDataType
    {
        Damage,
        Healing,
        DamageTaken
    }
    public enum SortingOption
    {
        BySource,
        ByAbility,
        ByTarget
    }
    public class TableInstanceViewModel : INotifyPropertyChanged
    {
        private SortingOption sortingOption;

        public SortingOption SortingOption { get => sortingOption; set
            { 
                sortingOption = value;
                Update();
            } 
        }
        public TableDataType Type { get; set; }
        public List<CombatInfoInstance> DataToView { get; set; }
        public Combat SeletedCombat;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public TableInstanceViewModel(TableDataType type)
        {
            Type = type;
        }
        public void DisplayNewData(Combat combat)
        {
            SeletedCombat = combat;
            Update();
        }
        public void Reset()
        {
            DataToView = new List<CombatInfoInstance>();
            OnPropertyChanged("DataToView");
        }
        private void Update()
        {
            DataToView = new List<CombatInfoInstance>();
            switch (Type)
            {
                case TableDataType.Damage:
                    DisplayDamageData(SeletedCombat);
                    break;
                case TableDataType.Healing:
                    DisplayHealingData(SeletedCombat);
                    break;
                case TableDataType.DamageTaken:
                    DisplayDamageTakenData(SeletedCombat);
                    break;
            }
            OnPropertyChanged("DataToView");
        }
        private void DisplayDamageTakenData(Combat combat)
        {

            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.AllDamageTakenLogs);
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void DisplayHealingData(Combat combat)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.AllHealingLogs);
            foreach (var orderedKey in splitOutdata)
            {
                PoppulateRows(orderedKey);
            }
        }

        private void DisplayDamageData(Combat combat)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.AllDamageLogs);
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
                Rate = (int)(orderedKey.Value.Sum(v => v.Value.EffectiveDblValue)/SeletedCombat.DurationSeconds),
                Average = (int)orderedKey.Value.Average(v => v.Value.EffectiveDblValue),
                Max = (int)orderedKey.Value.Max(v => v.Value.EffectiveDblValue),
                Count = (int)orderedKey.Value.Count(),
                CritPercent = (orderedKey.Value.Count(v=>v.Value.WasCrit) / (double)orderedKey.Value.Count()) * 100d,
            });
        }

        private Dictionary<string, List<ParsedLogEntry>> GetDataSplitOut(Combat combat, List<ParsedLogEntry> logsInScope)
        {
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = new Dictionary<string, List<ParsedLogEntry>>();
            switch (SortingOption)
            {
                case SortingOption.ByAbility:
                    splitOutdata = combat.GetByAbility(logsInScope);
                    break;
                case SortingOption.BySource:
                    splitOutdata = combat.GetBySource(logsInScope);
                    break;
                case SortingOption.ByTarget:
                    splitOutdata = combat.GetByTarget(logsInScope);
                    break;
            }
            return splitOutdata;
        }
    }
}
