using MoreLinq;
using ScottPlot;
using SWTORCombatParser.DataStructures.RaidInfos;
using SWTORCombatParser.Model.HistoricalLogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
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
            var titleAxis = HistoryPlot.Plot.XAxis2;
            titleAxis.Label(label: "Battle History Metadata", size: 25);
            HistoryPlot.Plot.YAxis.Label(label: "Value", size: 15);
            HistoryPlot.Plot.XAxis.Label(label: "Date", size: 15);
            HistoryPlot.Plot.Style(dataBackground: Color.FromArgb(150, 10, 10, 10), figureBackground: Color.FromArgb(0, 10, 10, 10), grid: Color.FromArgb(100, 40, 40, 40));
            CombatsDuringHistory = combats.Where(c=>c.DurationSeconds > 60).ToList();
            LocalPlayersDuringHistory = MetaDataExtractor.GetLocalEntities(CombatsDuringHistory);
            OnPropertyChanged("LocalPlayersDuringHistory");
            _allEncountersDuringHistory = MetaDataExtractor.GetAllEncounters(CombatsDuringHistory);
            _allBossFightsDuringHisotry = MetaDataExtractor.GetAllBossesFromCombats(CombatsDuringHistory);
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
                BossesInSelectedEncounter = MetaDataExtractor.GetAllBossesFromCombats(CombatsDuringHistory.Where(c=>c.LocalPlayer == SelectedLocalEntity && c.ParentEncounter == SelectedEncounter).ToList());
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
                var maxAPM = filteredCombats.MaxBy(c => c.APM[SelectedLocalEntity]).First().APM[SelectedLocalEntity];
                var maxDPS = filteredCombats.MaxBy(c => c.EDPS[SelectedLocalEntity]).First().EDPS[SelectedLocalEntity];
                var maxHPS = filteredCombats.MaxBy(c => c.EHPS[SelectedLocalEntity]).First().EHPS[SelectedLocalEntity];
                var maxDTPS = filteredCombats.MaxBy(c => c.EDTPS[SelectedLocalEntity]).First().EDTPS[SelectedLocalEntity];
                var maxHTPS = filteredCombats.MaxBy(c => c.EHTPS[SelectedLocalEntity]).First().EHTPS[SelectedLocalEntity];
                foreach (var combat in filteredCombats)
                {
                    var logEntry = new HistoricalLogEntry()
                    {
                        Boss = combat.EncounterBossInfo,
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
                    //foreach (var participant in combat.CharacterParticipants)
                    //{

                    //    var logEntry = new HistoricalLogEntry()
                    //    {
                    //        Boss = combat.EncounterBossInfo,
                    //        Kill = combat.WasBossKilled,
                    //        Encounter = selectedEncounter.Name,
                    //        Character = participant.Name,
                    //        LocalPlayer = participant == SelectedLocalEntity,
                    //        Date = combat.StartTime,
                    //        Duration = (int)combat.DurationSeconds,
                    //        APM = combat.APM[participant],
                    //        APMMeter = combat.APM[participant] / maxDPS,
                    //        DPS = combat.EDPS[participant],
                    //        DPSMeter = combat.EDPS[participant] / maxDPS,
                    //        HPS = combat.EHPS[participant],
                    //        HPSMeter = combat.EHPS[participant] / maxHPS,
                    //        DTPS = combat.EDTPS[participant],
                    //        DTPSMeter = combat.EDTPS[participant] / maxDTPS,
                    //        HTPS = combat.EHTPS[participant],
                    //        HTPSMeter = combat.EHTPS[participant] / maxHTPS
                    //    };
                    //    logsToView.Add(logEntry);
                    //}
                }
                for(var i =0;i<logsToView.Count;i++)
                {
                    if (i % 2 == 0)
                        logsToView[i].RowBackground = System.Windows.Media.Brushes.WhiteSmoke;
                }
                DataToView = logsToView;

                OnPropertyChanged("DataToView");
                HistoryPlot.Plot.Clear();

                foreach(var participant in DataToView.Select(d => d.Character).Distinct())
                {
                    var characterValues = DataToView.Where(d => d.Character == participant);
                    var xValues = characterValues.Select(d => (double)d.Date.ToOADate()).ToArray();
                    var durations = characterValues.Select(d => d.Duration).ToList();
                    AddBubbleSeries(xValues, characterValues.Select(d => d.DPS).ToArray(), "DPS", Color.PaleVioletRed, durations);
                    AddBubbleSeries(xValues, characterValues.Select(d => d.HPS).ToArray(), "HPS", Color.Green, durations);
                    AddBubbleSeries(xValues, characterValues.Select(d => d.DTPS).ToArray(), "DTPS", Color.Peru, durations);
                    AddBubbleSeries(xValues, characterValues.Select(d => d.HTPS).ToArray(), "HTPS", Color.CornflowerBlue, durations);
                }

                HistoryPlot.Plot.Legend();
                HistoryPlot.Plot.XAxis.DateTimeFormat(true);
                HistoryPlot.Refresh();
            }
        }
        public void AddBubbleSeries(double[] xValues, double[] yvalues, string label, Color color, List<int> durations)
        {

            var newBubblePlot = HistoryPlot.Plot.AddBubblePlot();
            for (var i = 0; i < xValues.Length; i++)
            {
                newBubblePlot.Add(
                    x: xValues[i],
                    y: yvalues[i],
                    fillColor: color,
                    radius: Math.Max(2, 15 * (durations[i] / 400d)),
                    edgeColor: Color.DimGray,
                    edgeWidth: 1
                    );
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
