using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser
{
    public class CombatLogFile
    {
        public string Data;
        public DateTime Time;
        public string Name;
    }
    public static class CombatLogLoader
    {
        private static string _logPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");

        public static CombatLogFile[] LoadAllCombatLogs()
        {
            if (!Directory.Exists(_logPath))
                return new CombatLogFile[0];
            var filePaths = Directory.GetFiles(_logPath);
            CombatLogFile[] combatLogsData = new CombatLogFile[filePaths.Length];
            Parallel.For(0, filePaths.Length, i => 
            {
                var filePath = filePaths[i];
                combatLogsData[i] = LoadCombatLog(filePath);
            });

            return combatLogsData.Where(l=>l.Data!="").OrderByDescending(v => v.Time).ToArray();
        }
        public static LogContents GetMostRecentCombats()
        {
            foreach(var log in LoadAllCombatLogs().Where(l => l.Data != ""))
            {
                var currentLogs = CombatLogParser.ParseAllLines(log);
                var check = currentLogs.Any(l => l.Effect.EffectName == "EnterCombat");
                if (check)
                    return CombatIdentifier.GetActiveCombatLogs(currentLogs);
            }
            return null;
        }
             
        public static CombatLogFile LoadMostRecentLog()
        {
            return LoadAllCombatLogs()[0];
        }
        public static CombatLogFile LoadMostRecentPopulatedLog()
        {
            return LoadAllCombatLogs().Where(l => l.Data != "").ToList()[0];
        }
        public static CombatLogFile LoadSpecificLog(string logPath)
        {
            return LoadCombatLog(logPath);
        }
        private static CombatLogFile LoadCombatLog(string path)
        {
            CombatLogFile fileData = new CombatLogFile();
            fileData.Name = Path.GetFileName(path);
            fileData.Time = new FileInfo(path).LastWriteTime;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.Default))
            {
                fileData.Data = sr.ReadToEnd();
            }
            return fileData;
        }
    }
}
