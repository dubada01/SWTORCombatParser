using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace SWTORCombatParser.ViewModels.HistoricalLogs
{
    public class HistoricalLogEntry
    {
        public SolidColorBrush RowBackround { get; set; }
        public DateTime Date { get; set; }
        public int Duration { get; set; }
        public string Encounter { get; set; }
        public string Boss { get; set; }
        public string Character { get; set; }
        public double DPS { get; set; }
        public double HPS { get; set; }
        public double DTPS { get; set; }
        public double HTPS { get; set; }
        public bool Kill { get; set; }
    }
}
