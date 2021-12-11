using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.HistoricalLogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.HistoricalLogs
{
    public class HistoricalCombatViewModel : INotifyPropertyChanged, IDisposable
    {
        private List<EncounterInfo> _allEncountersDuringHistory;
        private List<string> _allBossFightsDuringHisotry;
        private Entity selectedLocalEntity;
        private EncounterInfo selectedEncounter;
        private string selectedBoss;

        public HistoricalCombatViewModel(List<Combat> combats)
        {
            HistoryPlot = new WpfPlot();
            CombatsDuringHistory = combats;
            LocalPlayersDuringHistory = MetaDataExtractor.GetLocalEntities(CombatsDuringHistory);
            OnPropertyChanged("LocalPlayersDuringHistory");
            _allEncountersDuringHistory = MetaDataExtractor.GetAllEncounters(CombatsDuringHistory);
            _allBossFightsDuringHisotry = MetaDataExtractor.GetAllBossesFromEncounters(_allEncountersDuringHistory);
        }
        public WpfPlot HistoryPlot { get; set; }
        public List<HistoricalLogEntry> DataToView { get; set; }
        public List<Combat> CombatsDuringHistory { get; set; }
        public List<Entity> LocalPlayersDuringHistory { get; set; }
        public Entity SelectedLocalEntity
        {
            get => selectedLocalEntity;
            set
            {
                selectedLocalEntity = value;
                BossesInSelectedEncounter = new List<string>();
                EncountersForSeletedPlayer = MetaDataExtractor.GetEncountersForCharacter(CombatsDuringHistory, SelectedLocalEntity);
                OnPropertyChanged("EncountersForSeletedPlayer");
            }
        }

        public List<EncounterInfo> EncountersForSeletedPlayer { get; set; }
        public EncounterInfo SelectedEncounter
        {
            get => selectedEncounter;
            set
            {
                selectedEncounter = value;
                if (selectedEncounter == null)
                    return;
                BossesInSelectedEncounter = MetaDataExtractor.GetAllBossesFromEncounters(new List<EncounterInfo> { selectedEncounter });
                OnPropertyChanged("BossesInSelectedEncounter");
            }
        }
        public List<string> BossesInSelectedEncounter { get; set; }
        public string SelectedBoss
        {
            get => selectedBoss;
            set
            {
                selectedBoss = value;
                if (selectedBoss == null)
                    return;
                var filteredCombats = CombatsDuringHistory.Where(c => c.LocalPlayer == SelectedLocalEntity && c.EncounterBossInfo.Contains(selectedBoss));
                var logsToView = new List<HistoricalLogEntry>();
                foreach(var combat in filteredCombats)
                {
                    //foreach(var participant in combat.CharacterParticipants)
                    //{
                        var logEntry = new HistoricalLogEntry() { Boss = combat.EncounterBossInfo, Encounter = selectedEncounter.Name, Character = SelectedLocalEntity.Name, Date = combat.StartTime,Duration = (int)combat.DurationSeconds, DPS = combat.EDPS[SelectedLocalEntity], HPS = combat.EHPS[SelectedLocalEntity], DTPS = combat.EDTPS[SelectedLocalEntity], HTPS = combat.EHTPS[SelectedLocalEntity] };
                        logsToView.Add(logEntry);
                    //}
                }

                DataToView = logsToView;
                OnPropertyChanged("DataToView");
                HistoryPlot.Plot.Clear();
                HistoryPlot.Plot.AddScatter(DataToView.Select(d => (double)d.Date.ToOADate()).ToArray(), DataToView.Select(d => d.DPS).ToArray(),label:"DPS");
                HistoryPlot.Plot.AddScatter(DataToView.Select(d => (double)d.Date.ToOADate()).ToArray(), DataToView.Select(d => d.HPS).ToArray(), label: "HPS");
                HistoryPlot.Plot.AddScatter(DataToView.Select(d => (double)d.Date.ToOADate()).ToArray(), DataToView.Select(d => d.DTPS).ToArray(), label: "DTPS");
                HistoryPlot.Plot.AddScatter(DataToView.Select(d => (double)d.Date.ToOADate()).ToArray(), DataToView.Select(d => d.HTPS).ToArray(), label: "HTPS");
                HistoryPlot.Plot.Legend();
                HistoryPlot.Plot.XAxis.DateTimeFormat(true);
                HistoryPlot.Refresh();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Dispose()
        {
            CombatsDuringHistory.Clear();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
