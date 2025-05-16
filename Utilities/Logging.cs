using Newtonsoft.Json;
using SWTORCombatParser.Model.CloudLogging;
using System;
using System.IO;

namespace SWTORCombatParser.Utilities
{
    public class LoggingConfig
    {
        public bool verbose { get; set; }
    }
    public static class Logging
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DubaTech", "SWTORCombatParser");
        private static string _logPath = Path.Combine(appDataPath, "logs.txt");
        private static object _logLock = new object();

        private static bool _useVerboseLogging;
        public static void LogError(string message, bool tryCloudLogs = true)
        {
            LoadLoggingConfig();
            lock (_logLock)
            {
                if (tryCloudLogs && !Settings.ReadSettingOfType<bool>("offline_mode"))
                    CloudLogging.UploadLogAsync(message, "error");
                InitLogFile();
                using (StreamWriter sw = new StreamWriter(_logPath, true))
                {
                    sw.WriteLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff") + "----- ERROR ----- " + ": " + message + '\n');
                }
            }
        }
        public static void LogInfo(string message, bool isVerbose = true, bool tryCloudLogs = true)
        {
            LoadLoggingConfig();
            if (isVerbose && !_useVerboseLogging)
                return;
            lock (_logLock)
            {
                if (tryCloudLogs && !Settings.ReadSettingOfType<bool>("offline_mode"))
                    CloudLogging.UploadLogAsync(message, "info");
                InitLogFile();
                using (StreamWriter sw = new StreamWriter(_logPath, true))
                {
                    sw.WriteLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff") + "----- INFO ------ " + ": " + message + '\n');
                }
            }
        }

        public static void LogStartup()
        {
            LoadLoggingConfig();
            if (!_useVerboseLogging)
                return;
            lock (_logLock)
            {
                if (!Settings.ReadSettingOfType<bool>("offline_mode"))
                    CloudLogging.UploadLogAsync("User Started Orbs", "startup");
            }
        }
        public static void LoadLoggingConfig()
        {
            try
            {
                _useVerboseLogging = JsonConvert.DeserializeObject<LoggingConfig>(File.ReadAllText(@"LoggingConfig.json")).verbose;
            }
            catch (Exception exception)
            {
                LogError("ERROR: Could not load logging config.\r\n"+ exception.Message);
                //LogError("Failed to determine logging configuration. Please close and save LoggingConfig.json");
            }

        }
        private static void InitLogFile()
        {
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            if (!File.Exists(_logPath))
            {
                File.Create(_logPath).Close();
            }
        }
    }
}
