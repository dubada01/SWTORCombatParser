using SWTORCombatParser.Model.LogParsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Parsely
{
    public static class CombatExtractor
    {
        public static string GetCombatLinesForCombat(int startLine, int endLine, string combatLogFile)
        {
            var recentLog = CombatLogLoader.LoadSpecificLog(Path.Combine(Settings.ReadSettingOfType<string>("combat_logs_path"), combatLogFile));
            var combatLines = CombatLogParser.ExtractSpecificLines(recentLog, startLine, endLine);
            return string.Join("", combatLines);
        }
    }
}
