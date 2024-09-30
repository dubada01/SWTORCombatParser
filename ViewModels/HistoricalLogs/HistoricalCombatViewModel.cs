using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.HistoricalLogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace SWTORCombatParser.ViewModels.HistoricalLogs
{
    public class HistoricalCombatViewModel : INotifyPropertyChanged, IDisposable
    {
        private List<EncounterInfo> _allEncountersDuringHistory;
        private List<string> _allBossFightsDuringHisotry;
        private Entity selectedLocalEntity;
        private EncounterInfo selectedEncounter;
        private string selectedBoss;
        private double maxCombatLength = 400;

        public HistoricalCombatViewModel(List<Combat> combats)
        {
            CombatsDuringHistory = combats.Where(c => c.DurationSeconds > 60).ToList();
            //LocalPlayersDuringHistory = MetaDataExtractor.GetLocalEntities(CombatsDuringHistory);
            //OnPropertyChanged("LocalPlayersDuringHistory");
            _allEncountersDuringHistory = MetaDataExtractor.GetAllEncounters(CombatsDuringHistory);
            AllEncounters = _allEncountersDuringHistory.OrderBy(e => e.NamePlus).ToList();
            OnPropertyChanged("AllEncounters");
            _allBossFightsDuringHisotry = MetaDataExtractor.GetAllBossesFromCombats(CombatsDuringHistory);
        }
        public List<HistoricalLogEntry> DataToView { get; set; }
        public double MaxCombatLength
        {
            get => maxCombatLength;
            set
            {
                maxCombatLength = value;
            }
        }
        public List<Combat> CombatsDuringHistory { get; set; }
        public List<Entity> LocalPlayersDuringHistory { get; set; }
        public Entity SelectedLocalEntity
        {
            get => selectedLocalEntity;
            set
            {
                selectedLocalEntity = value;
                if (selectedLocalEntity == null)
                    return;
                MakeFinalSelection();
                //BossesInSelectedEncounter = new List<string>();
                //AllEncounters = MetaDataExtractor.GetEncountersForCharacter(CombatsDuringHistory, SelectedLocalEntity);
                //OnPropertyChanged("EncountersForSeletedPlayer");
            }
        }

        public List<EncounterInfo> AllEncounters { get; set; }
        public EncounterInfo SelectedEncounter
        {
            get => selectedEncounter;
            set
            {
                selectedEncounter = value;
                if (selectedEncounter == null)
                    return;
                BossesInSelectedEncounter = MetaDataExtractor.GetAllBossesFromCombats(CombatsDuringHistory.Where(c => c.ParentEncounter.NamePlus == SelectedEncounter.NamePlus).ToList());
                SelectedLocalEntity = null;
                OnPropertyChanged("BossesInSelectedEncounter");
                OnPropertyChanged("SelectedLocalEntity");
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
                LocalPlayersDuringHistory = MetaDataExtractor.GetPlayersForBossInEncounter(CombatsDuringHistory, SelectedEncounter, SelectedBoss);
                if (LocalPlayersDuringHistory.Contains(SelectedLocalEntity))
                    MakeFinalSelection();
                else
                    SelectedLocalEntity = null;
                OnPropertyChanged("LocalPlayersDuringHistory");
                OnPropertyChanged("SelectedLocalEntity");

            }
        }

        private void MakeFinalSelection()
        {
            var filteredCombats = CombatsDuringHistory.Where(c => c.LocalPlayer == SelectedLocalEntity && c.ParentEncounter.NamePlus == SelectedEncounter.NamePlus && c.EncounterBossDifficultyParts.Item1 == SelectedBoss);
            var logsToView = new List<HistoricalLogEntry>();
            var maxAPM = filteredCombats.MaxBy(c => c.APM[SelectedLocalEntity]).APM[SelectedLocalEntity];
            var maxDPS = filteredCombats.MaxBy(c => c.EDPS[SelectedLocalEntity]).EDPS[SelectedLocalEntity];
            var maxHPS = filteredCombats.MaxBy(c => c.EHPS[SelectedLocalEntity]).EHPS[SelectedLocalEntity];
            var maxDTPS = filteredCombats.MaxBy(c => c.EDTPS[SelectedLocalEntity]).EDTPS[SelectedLocalEntity];
            var maxHTPS = filteredCombats.MaxBy(c => c.EHTPS[SelectedLocalEntity]).EHTPS[SelectedLocalEntity];
            foreach (var combat in filteredCombats)
            {
                var logEntry = new HistoricalLogEntry()
                {
                    Boss = combat.EncounterBossDifficultyParts.Item1,
                    Kill = combat.WasBossKilled,
                    Encounter = selectedEncounter.Name,
                    Character = SelectedLocalEntity.Name,
                    LocalPlayer = SelectedLocalEntity == SelectedLocalEntity,
                    Date = combat.StartTime,
                    Duration = (int)combat.DurationSeconds,
                    APM = combat.APM[SelectedLocalEntity],
                    APMMeter = combat.APM[SelectedLocalEntity] / maxAPM,
                    DPS = combat.EDPS[SelectedLocalEntity],
                    DPSMeter = combat.EDPS[SelectedLocalEntity] / maxDPS,
                    HPS = combat.EHPS[SelectedLocalEntity],
                    HPSMeter = combat.EHPS[SelectedLocalEntity] / maxHPS,
                    DTPS = combat.EDTPS[SelectedLocalEntity],
                    DTPSMeter = combat.EDTPS[SelectedLocalEntity] / maxDTPS,
                    HTPS = combat.EHTPS[SelectedLocalEntity],
                    HTPSMeter = combat.EHTPS[SelectedLocalEntity] / maxHTPS
                };
                logsToView.Add(logEntry);
            }

            DataToView = logsToView.OrderByDescending(c => c.Date).ToList();
            for (var i = 0; i < DataToView.Count; i++)
            {
                if (i % 2 == 0)
                    DataToView[i].RowBackground = new SolidColorBrush(Colors.DimGray);
            }
            OnPropertyChanged("DataToView");

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