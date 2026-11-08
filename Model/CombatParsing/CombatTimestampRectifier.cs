using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;

namespace SWTORCombatParser.Model.CombatParsing
{
    public static class CombatTimestampRectifier
    {
        public static void RectifyTimeStamps(List<ParsedLogEntry> parsedLog)
        {
            // Parsed timestamps initially share the file's start date. A large
            // backwards jump in the time of day marks a transition at midnight;
            // small out-of-order timestamps should not advance the date.
            TimeSpan? previousTime = null;
            var dayOffset = 0;
            foreach (var entry in parsedLog)
            {
                if (entry == null || entry.Error != ErrorType.None || entry.TimeStamp == DateTime.MinValue)
                    continue;

                var time = entry.TimeStamp.TimeOfDay;
                if (previousTime.HasValue && previousTime.Value - time > TimeSpan.FromHours(12))
                    dayOffset++;

                previousTime = time;
                if (dayOffset != 0)
                    entry.TimeStamp = entry.TimeStamp.AddDays(dayOffset);
            }
        }
    }
}
