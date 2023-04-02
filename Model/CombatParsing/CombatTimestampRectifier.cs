using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatTimestampRectifier
    {
        public static void RectifyTimeStamps(List<ParsedLogEntry> parsedLog)
        {
            var ndx = parsedLog.FindIndex(l => l.Error == ErrorType.None && l.LogLineNumber+1 < parsedLog.Count && l.TimeStamp > parsedLog[(int)l.LogLineNumber + 1].TimeStamp);
            if (ndx != -1)
            {
                var previousDaysLogs = parsedLog.GetRange(ndx, (parsedLog.Count-ndx));
                previousDaysLogs.ForEach(l => l.TimeStamp = l.TimeStamp.AddDays(1));
            }
        }
    }
}
