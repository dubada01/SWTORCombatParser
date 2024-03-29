﻿using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
        public ImageSource Icon { get; set; }
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
        internal override async void Update()
        {
            if (_selectedEntity == null || SelectedCombat == null)
                return;
            DataToView = new List<CombatInfoInstance>();
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
                list.Add(new CombatInfoInstance
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
            for (var i = 0; i < list.Count; i++)
            {
                if (i % 2 == 1)
                {
                    list[i].RowBackground = (SolidColorBrush)App.Current.FindResource("Gray4Brush");
                }
            }
            App.Current.Dispatcher.Invoke(() => {
                DataToView = list;
                OnPropertyChanged("DataToView");
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
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.IncomingDamageLogs[defaultEntity]);
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                await PoppulateRows(orderedKey, list);
            }
        }

        private async Task DisplayHealingData(Combat combat, List<CombatInfoInstance> list)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            var healing = combat.OutgoingHealingLogs[defaultEntity];
            var shielding = combat.ShieldingProvidedLogs[defaultEntity];
            var both = healing.Concat(shielding);
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, both.ToList());
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                await PoppulateRows(orderedKey, list);
            }
        }

        private async Task DisplayDamageData(Combat combat, List<CombatInfoInstance> list)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.OutgoingDamageLogs[defaultEntity]);
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                await PoppulateRows(orderedKey, list);
            }
        }
        private async Task DisplayHealingReceived(Combat combat, List<CombatInfoInstance> list)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.IncomingHealingLogs[defaultEntity]);
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Sum(v => v.Value.EffectiveDblValue));
            foreach (var orderedKey in splitOutdata)
            {
                await PoppulateRows(orderedKey, list);
            }
        }
        private async Task DisplayThreat(Combat combat, List<CombatInfoInstance> list)
        {
            var defaultEntity = combat.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : combat.OutgoingDamageLogs.Keys.First();
            Dictionary<string, List<ParsedLogEntry>> splitOutdata = GetDataSplitOut(combat, combat.LogsInvolvingEntity[defaultEntity].Where(l=>l.Source == defaultEntity && l.Threat != 0).ToList());
            _sumTotal = splitOutdata.Sum(kvp => kvp.Value.Where(v=>v.Threat >=0).Sum(v => v.Threat));
            foreach (var orderedKey in splitOutdata)
            {
                await PoppulateRowsThreat(orderedKey, list);
            }
        }
        private async Task PoppulateRowsThreat(KeyValuePair<string, List<ParsedLogEntry>> orderedKey, List<CombatInfoInstance> list)
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
        private async Task PoppulateRows(KeyValuePair<string, List<ParsedLogEntry>> orderedKey, List<CombatInfoInstance> list)
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
        private async Task<ImageSource> GetIconForRow(ParsedLogEntry log)
        {
            if(log == null) return null;
            var sourceClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(log.Source, log.TimeStamp);
            var targetClass = CombatLogStateBuilder.CurrentState.GetCharacterClassAtTime(log.Target, log.TimeStamp);
            switch (sortingOption)
            {
                case SortingOption.ByAbility:
                    return await IconGetter.GetIconPathForLog(log);
                case SortingOption.BySource:
                    return IconFactory.GetColoredBitmapImage(sourceClass.Name, GetIconColorFromClass(sourceClass));
                case SortingOption.ByTarget:
                    return IconFactory.GetColoredBitmapImage(targetClass.Name, GetIconColorFromClass(targetClass));
                default:
                    return null;
            }
        }
        private Color GetIconColorFromClass(SWTORClass classInfo)
        {
            return classInfo.Role switch
            {
                Role.Healer => Colors.ForestGreen,
                Role.Tank => Colors.CornflowerBlue,
                Role.DPS => Colors.IndianRed,
                _ => (Color)ResourceFinder.GetColorFromResourceName("Gray4")
            };
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
