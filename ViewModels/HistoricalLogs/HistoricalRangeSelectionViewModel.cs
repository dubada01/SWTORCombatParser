using MoreLinq;
using SWTORCombatParser.Model.HistoricalLogs;
using SWTORCombatParser.resources;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.HistoricalLogs
{
    public class HistoricalRangeSelectionViewModel : INotifyPropertyChanged
    {
        private DateTime fromDate = DateTime.Now.AddDays(-1);
        private DateTime toDate = DateTime.Now;

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
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public ICommand FetchHistoryBetweenDatesCommand => new CommandHandler(FetchHistoryBetweenDates);

        private void FetchHistoryBetweenDates(object obj)
        {
            var window = LoadingWindowFactory.ShowLoading();
            Task.Run(() => {
                var streamer = new CombatLogStreamer();
                var combatFiles = CombatLogLoader.LoadCombatsBetweenTimes(FromDate,ToDate);
                var combatsWithinRange = combatFiles.Where(c => c.Time > FromDate && c.Time <= ToDate);
                var allCombats = new List<Combat>();
                foreach (var combatLog in combatsWithinRange)
                {
                    var combatLines = CombatLogParser.ParseAllLines(combatLog);
                    
                    var combats = CombatIdentifier.GetAllBossCombatsFromLog(combatLines);
                    if (combats.Count == 0)
                        continue;
                    allCombats.AddRange(combats);
                    window.SetString($"Identified {allCombats.Count.ToString("#,0")} combats");

                }
                HistoricalCombatsParsed(allCombats);
                LoadingWindowFactory.HideLoading();
            });

        }
    }
}
