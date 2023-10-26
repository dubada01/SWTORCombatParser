using ScottPlot;
using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace SWTORCombatParser.ViewModels.Overviews
{
    public class HistogramInstanceViewModel : OverviewInstanceViewModel, INotifyPropertyChanged
    {
        private Dictionary<string, Dictionary<string, List<double>>> _combatDatas;
        private Dictionary<string, Combat> _selectedCombats;
        private string selectedAbility;
        private SortingOption sortingOption;

        public override SortingOption SortingOption { get { return sortingOption; } set { sortingOption = value; } }
        public string SelectedAbility
        {
            get => selectedAbility; set
            {
                if (selectedAbility == value)
                    return;
                selectedAbility = value;

                Update();
            }
        }

        public List<string> AvailableAbilities { get; set; }
        public WpfPlot HistogramPlot { get; set; }
        public HistogramInstanceViewModel(OverviewDataType type) : base(type)
        {
            HistogramPlot = new WpfPlot();
            HistogramPlot.Plot.Style(dataBackground: Color.FromArgb(150, 20, 20, 20), figureBackground: Color.FromArgb(0, 20, 20, 20), grid: Color.FromArgb(100, 40, 40, 40));
            OnPropertyChanged("HistogramPlot");
            AvailableAbilities = new List<string>();
            _selectedCombats = new Dictionary<string, Combat>();
            _combatDatas = new Dictionary<string, Dictionary<string, List<double>>>();
        }
        public override void UpdateData(Combat combat)
        {
            AvailableAbilities = new List<string>();
            _selectedCombats[combat.StartTime.ToString()] = combat;
            Update();
        }
        public override void Reset()
        {
            AvailableAbilities.Clear();
            _selectedEntity = null;
            HistogramPlot.Plot.Clear();
            _selectedCombats = new Dictionary<string, Combat>();
            _combatDatas = new Dictionary<string, Dictionary<string, List<double>>>();
        }
        internal override void UpdateParticipant()
        {
            AvailableAbilities.Clear();
            HistogramPlot.Plot.Clear();
            Update();
        }
        private void PlotData()
        {
            HistogramPlot.Plot.Clear();
            foreach (var kvp in _combatDatas)
            {
                var abilityData = kvp.Value;
                if (string.IsNullOrEmpty(SelectedAbility) || !abilityData.ContainsKey(SelectedAbility))
                    continue;
                var combatTag = kvp.Key;
                var selectedAbilityData = abilityData[SelectedAbility];
                var binSize = Math.Max(1, (selectedAbilityData.Max() - selectedAbilityData.Min()) / 50);
                (double[] counts, double[] binEdges) = ScottPlot.Statistics.Common.Histogram(selectedAbilityData.ToArray(), min: selectedAbilityData.Min() - 20, max: selectedAbilityData.Max() + 20, binSize);
                double[] leftEdges = binEdges.Take(binEdges.Length - 1).ToArray();
                var barPlot = HistogramPlot.Plot.AddBar(counts, leftEdges);
                barPlot.FillColor = Color.FromArgb(100, barPlot.FillColor);
                barPlot.Label = combatTag;
                barPlot.BarWidth = binSize;
                barPlot.BorderColor = ColorTranslator.FromHtml("#82add9");

            }
            HistogramPlot.Plot.Legend();
            HistogramPlot.Refresh();
        }
        internal override void Update()
        {
            if (_selectedEntity == null)
                return;

            foreach (var kvp in _selectedCombats)
            {
                switch (_type)
                {
                    case OverviewDataType.Damage:
                        DisplayDamageData(kvp);
                        break;
                    case OverviewDataType.Healing:
                        DisplayHealingData(kvp);
                        break;
                    case OverviewDataType.DamageTaken:
                        DisplayDamageTakenData(kvp);
                        break;
                    case OverviewDataType.HealingReceived:
                        DisplayHealingReceived(kvp);
                        break;
                }
            }
            PlotData();
        }

        private void DisplayDamageTakenData(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            var defaultEntity = comb.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : comb.OutgoingDamageLogs.Keys.First();
            Display(comb.GetByAbility(comb.IncomingDamageLogs[defaultEntity]), combat.Key);
        }

        private void DisplayHealingData(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            var defaultEntity = comb.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : comb.OutgoingDamageLogs.Keys.First();
            Display(comb.GetByAbility(comb.OutgoingHealingLogs[defaultEntity]), combat.Key);
        }

        private void DisplayDamageData(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            var defaultEntity = comb.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : comb.OutgoingDamageLogs.Keys.First();
            Display(comb.GetByAbility(comb.OutgoingDamageLogs[defaultEntity]), combat.Key);
        }
        private void DisplayHealingReceived(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            var defaultEntity = comb.OutgoingDamageLogs.ContainsKey(_selectedEntity) ? _selectedEntity : comb.OutgoingDamageLogs.Keys.First();
            Display(comb.GetByAbility(comb.IncomingHealingLogs[defaultEntity]), combat.Key);

        }
        private void Display(Dictionary<string, List<ParsedLogEntry>> data, string combatId)
        {
            if (data.Count == 0)
                return;
            var orderedData = data.OrderByDescending(kvp => kvp.Value.Select(v => v.Value.DblValue).Sum()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            if (AvailableAbilities.Count == 0)
            {
                AvailableAbilities = orderedData.Keys.ToList();
                SelectedAbility = AvailableAbilities[0];
            }
            else
            {
                AvailableAbilities.AddRange(orderedData.Keys);
                AvailableAbilities = AvailableAbilities.Distinct().ToList();
            }
            OnPropertyChanged("AvailableAbilities");
            OnPropertyChanged("SelectedAbility");
            PoppulateRows(data, combatId);
        }
        private void PoppulateRows(Dictionary<string, List<ParsedLogEntry>> abilityCombatData, string combatKey)
        {
            var abilityCombatDict = new Dictionary<string, List<double>>();

            foreach (var kvp in abilityCombatData)
            {
                abilityCombatDict[kvp.Key] = kvp.Value.Select(v => v.Value.DblValue).ToList();
            }
            _combatDatas[combatKey] = abilityCombatDict;

        }
    }
}
