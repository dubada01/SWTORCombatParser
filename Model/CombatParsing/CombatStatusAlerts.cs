using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CombatParsing
{
    public enum UpdateType
    {
        Start,
        Stop,
        Update
    }
    public class CombatStatusUpdate
    {
        public UpdateType Type { get; set; }
        public List<ParsedLogEntry> Logs { get; set; }
        public string CombatLocation { get; set; }
        public DateTime CombatStartTime { get; set; }
    }
}
