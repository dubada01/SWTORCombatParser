using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SWTORCombatParser
{
    public static class CombatIdentifier
    {
        public static Combat ParseOngoingCombat(List<ParsedLogEntry> ongoingLogs)
        {
            var newCombat = new Combat()
            {
                StartTime = ongoingLogs.First().TimeStamp,
                EndTime = ongoingLogs.Last().TimeStamp,
                Targets = GetTargets(ongoingLogs),
                Logs = ongoingLogs
            };
            CombatMetaDataParse.PopulateMetaData(ref newCombat);
            return newCombat;
        }
        private static List<string> GetTargets(List<ParsedLogEntry> logs)
        {
            return logs.Select(l=>l.Target).Where(t=>!t.IsCharacter).Select(npc=>npc.Name).Distinct().ToList();
        }
    }
}
