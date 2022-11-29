using System;
using System.IO;
using System.Linq;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.Utilities
{
    public static class LocalCombatLogCaching
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser", "LocalCombats");
        public static void SaveCombatLogs(Combat swtorCombat, bool wasLive)
        {
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            //if (!swtorCombat.IsCombatWithBoss)
            //    return;
            var encounter = swtorCombat.ParentEncounter != null ? swtorCombat.ParentEncounter.Name : "Open_World";
            var bossName = swtorCombat.EncounterBossInfo != null ? swtorCombat.EncounterBossInfo : string.Join("_", swtorCombat.Targets.Select(t => t.Name));
            var fileName = Path.Combine(appDataPath, encounter + "_" + bossName + "_" + (wasLive ? "live" : "historical") + "_" + swtorCombat.StartTime.ToString("MM-dd-yy hh.mm.ss") +".combatLog");
            var file = File.Create(fileName);
            file.Close();
            var fileStream = new StreamWriter(fileName);
            foreach(var line in swtorCombat.AllLogs)
            {
                fileStream.WriteLine(line.LogText);
            }
            fileStream.Flush();
            fileStream.Close();
        }
    }
}
