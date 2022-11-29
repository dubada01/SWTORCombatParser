using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatTimestampRectifier
    {
        public static void RectifyTimeStamps(List<ParsedLogEntry> parsedLog)
        {
            var dateChangeIndex = parsedLog.Where(l=>l.Error == ErrorType.None).OrderBy(l => l.LogLineNumber).SkipLast(1).Select((l, i) => l.TimeStamp > parsedLog[i + 1].TimeStamp).ToList();
            if (dateChangeIndex.Any(d => d))
            {
                var ndx = dateChangeIndex.IndexOf(true);
                var previousDaysLogs = parsedLog.Take(ndx+1);
                previousDaysLogs.ToList().ForEach(l => l.TimeStamp = l.TimeStamp.AddDays(-1));
            }
        }
    }
}
