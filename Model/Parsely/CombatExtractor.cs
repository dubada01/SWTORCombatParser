using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Parsely
{
    public static class CombatExtractor
    {
        public static string GetCombatLinesForCombat(int startLine, int endLine)
        {
            var recentLog = CombatLogLoader.LoadMostRecentLog();
            var combatLines = CombatLogParser.ExtractSpecificLines(recentLog, startLine, endLine);
            return string.Join("", combatLines);
        }
    }
}
