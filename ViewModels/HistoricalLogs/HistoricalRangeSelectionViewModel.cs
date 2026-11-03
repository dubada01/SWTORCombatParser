using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.HistoricalLogs;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.HistoricalLogs
{
    public class HistoricalRangeSelectionViewModel :ReactiveObject
    {
        private DateTime fromDate = DateTime.Today.AddDays(-1);
        private DateTime toDate = DateTime.Today;

        public event Action<List<Combat>> HistoricalCombatsParsed = delegate { };
        public DateTime FromDate
        {
            get => fromDate; set
            {
                if (value > DateTime.Now)
                {
                    fromDate = DateTime.Now;
                    return;
                }
                if (value > ToDate)
                {
                    fromDate = ToDate;
                    return;
                }
                fromDate = value;
            }
        }
        public DateTime ToDate
        {
            get => toDate; set
            {
                if (value > DateTime.Now)
                {
                    toDate = DateTime.Now;
                    return;
                }
                if (value < FromDate)
                {
                    toDate = FromDate;
                    return;
                }

                toDate = value;

            }
        }
        public ReactiveCommand<Unit,Unit> FetchHistoryBetweenDatesCommand => ReactiveCommand.Create(FetchHistoryBetweenDates);

        private void FetchHistoryBetweenDates()
        {
            var window = LoadingWindowFactory.ShowLoading();
            Task.Run(() =>
            {
                var combatFiles = CombatLogLoader.LoadCombatsBetweenTimes(FromDate, ToDate.AddDays(1));
                var combatsWithinRange = combatFiles.Where(c => c.Time > FromDate && c.Time <= ToDate.AddDays(1));
                var allCombats = new List<Combat>();
                foreach (var combatLog in combatsWithinRange)
                {
                    var combatLines = CombatLogParser.ParseAllLines(combatLog);

                    var combats = SkimBossCombatsFromLogs.GetBossCombats(combatLines);
                    foreach (var combat in combats)
                    {
                        combat.StartTime = new DateTime(combatLog.Time.Year, combatLog.Time.Month, combatLog.Time.Day, combat.StartTime.Hour, combat.StartTime.Minute, combat.StartTime.Second, combat.StartTime.Millisecond);
                        combat.EndTime = new DateTime(combatLog.Time.Year, combatLog.Time.Month, combatLog.Time.Day, combat.EndTime.Hour, combat.EndTime.Minute, combat.EndTime.Second, combat.EndTime.Millisecond);
                    }
                    if (combats.Count == 0)
                        continue;
                    allCombats.AddRange(combats);
                    window.SetString($"Identified {allCombats.Count.ToString("#,0")} combats");

                }
                var uploadedCombats = 0;
                var distinctEncounters = allCombats.Select(c => c.ParentEncounter.NamePlus + c.EncounterBossDifficultyParts.Item1).Distinct();
                foreach (var encounter in allCombats)
                {
                    // var combat = allCombats.First(c => c.ParentEncounter.NamePlus + c.EncounterBossDifficultyParts.Item1 == encounter);
                    uploadedCombats++;
                    window.SetString($"Cached {uploadedCombats.ToString("#,0")} boss combats");
                }
                HistoricalCombatsParsed(allCombats);
                LoadingWindowFactory.HideLoading();
            });

        }
    }
}
