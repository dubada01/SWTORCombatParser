using ScottPlot;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace SWTORCombatParser.ViewModels.Histogram
{
    public class HistogramInstanceViewModel : INotifyPropertyChanged
    {
        private TableDataType _type;
        private Dictionary<string, Dictionary<string, List<double>>> _combatDatas;
        private Dictionary<string, Combat> _selectedCombats;
        private string selectedAbility;
        private Entity _selectedEntity;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
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

        internal void UpdateEntity(Entity selectedEntity)
        {
            _selectedEntity = selectedEntity;
        }

        public List<string> AvailableAbilities { get; set; }
        public WpfPlot HistogramPlot { get; set; }
        public HistogramInstanceViewModel(TableDataType type)
        {
            _type = type;
            HistogramPlot = new WpfPlot();
            HistogramPlot.Plot.Style(dataBackground: Color.FromArgb(150, 10, 10, 10), figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 40, 40, 40));
            OnPropertyChanged("HistogramPlot");
            AvailableAbilities = new List<string>();
            _selectedCombats = new Dictionary<string, Combat>();
            _combatDatas = new Dictionary<string, Dictionary<string, List<double>>>();
        }
        public void DisplayNewData(Combat combat)
        {
            AvailableAbilities = new List<string>();
            _selectedCombats[combat.StartTime.ToString()] = combat;
            Update();
        }
        public void UnselectCombat(Combat combat)
        {
            _selectedCombats.Remove(combat.StartTime.ToString());
            _combatDatas.Remove(combat.StartTime.ToString());
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
                var hist = new ScottPlot.Statistics.Histogram(selectedAbilityData.ToArray(), min: selectedAbilityData.Min() - 20, max: selectedAbilityData.Max() + 20);
                var barWidth = hist.binSize * 1.2d;
                var barPlot = HistogramPlot.Plot.AddBar(hist.counts, hist.bins);
                barPlot.FillColor = System.Drawing.Color.FromArgb(100, barPlot.FillColor);
                barPlot.BarWidth = barWidth;
                barPlot.Label = combatTag;
            }
            HistogramPlot.Plot.Legend();
        }
        private void Update()
        {
            if (_selectedEntity == null)
                return;
            foreach (var kvp in _selectedCombats)
            {
                switch (_type)
                {
                    case TableDataType.Damage:
                        DisplayDamageData(kvp);
                        break;
                    case TableDataType.Healing:
                        DisplayHealingData(kvp);
                        break;
                    case TableDataType.DamageTaken:
                        DisplayDamageTakenData(kvp);
                        break;
                    case TableDataType.HealingReceived:
                        DisplayHealingReceived(kvp);
                        break;
                }
            }
            PlotData();
            //OnPropertyChanged("DataToView");
        }
        private void DisplayDamageTakenData(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            Display(comb.GetByAbility(comb.IncomingDamageLogs[_selectedEntity]), combat.Key);
        }

        private void DisplayHealingData(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            Display(comb.GetByAbility(comb.OutgoingHealingLogs[_selectedEntity]), combat.Key);
        }

        private void DisplayDamageData(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            Display(comb.GetByAbility(comb.OutgoingDamageLogs[_selectedEntity]), combat.Key);
        }
        private void DisplayHealingReceived(KeyValuePair<string, Combat> combat)
        {
            var comb = combat.Value;
            Display(comb.GetByAbility(comb.IncomingHealingLogs[_selectedEntity]), combat.Key);

        }
        private void Display(Dictionary<string, List<ParsedLogEntry>> data, string combatId)
        {
            if (data.Count == 0)
                return;
            var orderedData = data.OrderByDescending(kvp => kvp.Value.Select(v=>v.Value.DblValue).Sum()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
