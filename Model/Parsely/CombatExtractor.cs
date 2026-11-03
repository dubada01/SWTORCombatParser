using SWTORCombatParser.Model.LogParsing;
using System.IO;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.Model.Parsely
{
    public static class CombatExtractor
    {
        public static string GetCombatLinesForCombat(int startLine, int endLine, string combatLogFile)
        {
            var recentLog = CombatLogLoader.LoadSpecificLog(Path.Combine(Settings.ReadSettingOfType<string>("combat_logs_path"), combatLogFile));
            var combatLines = CombatLogParser.ExtractSpecificLines(recentLog, startLine, endLine);
            return string.Join("\r\n", combatLines);
        }
    }
}
