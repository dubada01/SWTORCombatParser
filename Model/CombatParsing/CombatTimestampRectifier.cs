using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
                var previousDaysLogs = parsedLog.Take(ndx);
                previousDaysLogs.ToList().ForEach(l => l.TimeStamp = l.TimeStamp.AddDays(-1));
            }
        }
    }
}
