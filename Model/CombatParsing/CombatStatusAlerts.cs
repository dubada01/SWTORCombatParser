using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;

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
        public bool IsRealtime { get; set; }
    }
}
