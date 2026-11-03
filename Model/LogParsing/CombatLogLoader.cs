using SWTORCombatParser.Utilities;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.LogParsing
{
    public class CombatLogFile
    {
        public StreamReader Data;
        public DateTime Time;
        public string Name;
        public string Path;
    }
    public static class CombatLogLoader
    {
        public static string LoggingPath { get; set; }

        public static void RefreshSWTORCombatLogsDirectory()
        {
            LoggingPath = Settings.ReadSettingOfType<string>("combat_logs_path");
        }
        public static string GetLogDirectory()
        {
            return LoggingPath;
        }
        public static CombatLogFile[] LoadCombatsBetweenTimes(DateTime from, DateTime to)
        {
            if (!Directory.Exists(LoggingPath))
                return new CombatLogFile[0];
            var info = new DirectoryInfo(LoggingPath);
            var filePaths = info.GetFiles();
            var filesToUse = filePaths.Where(f => f.LastWriteTime > from && f.LastWriteTime <= to).ToList();
            CombatLogFile[] combatLogsData = new CombatLogFile[filesToUse.Count];
            Parallel.For(0, filesToUse.Count, i =>
            {
                var filePath = filesToUse[i];
                combatLogsData[i] = LoadCombatLog(filePath.FullName);
            });

            return combatLogsData.OrderByDescending(v => v.Time).ToArray();
        }
        public static CombatLogFile[] LoadAllCombatLogs()
        {
            if (!Directory.Exists(LoggingPath))
                return new CombatLogFile[0];
            var filePaths = Directory.GetFiles(LoggingPath);
            CombatLogFile[] combatLogsData = new CombatLogFile[filePaths.Length];
            Parallel.For(0, filePaths.Length, i =>
            {
                var filePath = filePaths[i];
                combatLogsData[i] = LoadCombatLog(filePath);
            });

            return combatLogsData.OrderByDescending(v => v.Time).ToArray();
        }
        public static bool CheckIfCombatLoggingPresent()
        {
            if (!Directory.Exists(LoggingPath))
            {
                return false;
            }
            return true;
        }
        public static bool CheckIfCombatLogsArePresent()
        {
            if (CheckIfCombatLoggingPresent())
            {
                var files = new DirectoryInfo(LoggingPath).EnumerateFiles();
                if (files.Count() > 0)
                    return true;
                return false;
            }
            return false;
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
            return LoadAllCombatLogs().ToList()[0];
        }
        public static CombatLogFile LoadSpecificLog(string logPath)
        {
            return LoadCombatLog(logPath);
        }
        private static string GetMostRecentCombatFile()
        {
            var files = new DirectoryInfo(LoggingPath).EnumerateFiles().Where(l=>l.Extension == ".txt");
            return files.OrderByDescending(f => f.LastWriteTime).ToList()[0].FullName;
        }
        private static CombatLogFile LoadCombatLog(string path)
        {
            Logging.LogInfo("Loading log - " + path);
            CombatLogFile fileData = new CombatLogFile();
            fileData.Name = Path.GetFileName(path);
            fileData.Path = path;
            fileData.Time = new FileInfo(path).LastWriteTime;
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var sr = new StreamReader(fs, Encoding.GetEncoding(1252), true);

            fileData.Data = sr;

            return fileData;
        }
    }
}
