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
        public string Path;
    }
    public static class CombatLogLoader
    {
        private static string _logPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        public static string GetLogDirectory()
        {
            return _logPath;
        }
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
        public static string GetMostRecentLogName()
        {
            return Path.GetFileName(GetMostRecentCombatFile());
        }
        public static string GetMostRecentLogPath()
        {
            return GetMostRecentCombatFile();
        }
        public static CombatLogFile LoadMostRecentLog()
        {   
            return LoadSpecificLog(GetMostRecentCombatFile());
        }
        public static CombatLogFile LoadMostRecentPopulatedLog()
        {
            return LoadAllCombatLogs().Where(l => l.Data != "").ToList()[0];
        }
        public static CombatLogFile LoadSpecificLog(string logPath)
        {
            return LoadCombatLog(logPath);
        }
        private static string GetMostRecentCombatFile()
        {
            var files = new DirectoryInfo(_logPath).EnumerateFiles();
            return files.OrderByDescending(f => f.LastWriteTime).ToList()[0].FullName;
        }
        private static CombatLogFile LoadCombatLog(string path)
        {
            CombatLogFile fileData = new CombatLogFile();
            fileData.Name = Path.GetFileName(path);
            fileData.Path = path;
            fileData.Time = new FileInfo(path).LastWriteTime;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF7))
            {
                fileData.Data = sr.ReadToEnd();
            }
            return fileData;
        }
    }
}
