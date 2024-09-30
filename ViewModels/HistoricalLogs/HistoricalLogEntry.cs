using System;
using Avalonia.Media;

namespace SWTORCombatParser.ViewModels.HistoricalLogs
{
    public class HistoricalLogEntry
    {

        public SolidColorBrush RowBackground { get; set; }
        public DateTime Date { get; set; }
        public int Duration { get; set; }
        public string Encounter { get; set; }
        public string Boss { get; set; }
        public string Character { get; set; }
        public bool LocalPlayer { get; set; }
        public double APM { get; set; }
        public double APMMeter { get; set; }
        public double DPS { get; set; }
        public double DPSMeter { get; set; }
        public double HPS { get; set; }
        public double HPSMeter { get; set; }
        public double DTPS { get; set; }
        public double DTPSMeter { get; set; }
        public double HTPS { get; set; }
        public double HTPSMeter { get; set; }
        public bool Kill { get; set; }

    }
}
