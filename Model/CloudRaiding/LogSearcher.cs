using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public static class LogSearcher
    {
        public static int GetIndexOfNextCombatEndLog(int combatStartIndex, List<ParsedLogEntry> logs)
        {
            for(var i = combatStartIndex; i < logs.Count; i++)
            {
                if (logs[i].Ability == "SWTOR_PARSING_COMBAT_END")
                    return i;
            }
            return logs.Count;
        }
    }
}
