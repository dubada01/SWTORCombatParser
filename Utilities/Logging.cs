using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public static class Logging
    {
        private static string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DubaTech", "SWTORCombatParser");
        private static string _logPath = Path.Combine(appDataPath, "logs.txt");
        private static object _logLock = new object();
        public static void LogError(string message)
        {
            lock (_logLock)
            {
                InitLogFile();
                using (StreamWriter sw = new StreamWriter(_logPath,true))
                {
                    sw.WriteLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff")+"----- ERROR ----- "+": "+message+'\n');
                }
            }
        }
        public static void LogInfo(string message)
        {
            lock (_logLock)
            {
                InitLogFile();
                using (StreamWriter sw = new StreamWriter(_logPath, true))
                {
                    sw.WriteLine(DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.fff")+"----- INFO ------ "+": "+message+'\n');
                }
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
