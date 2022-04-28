using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Utilities
{
    public class ParsingPerformanceProfiler
    {
        private static string _fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Orbs Bonus Data", "timingInfo.txt");
        private DateTime _startTime;
        
        public void StartLogProcessing()
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Orbs Bonus Data")))
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Orbs Bonus Data"));
            if (!File.Exists(_fileName))
                File.Create(_fileName).Close();
            _startTime = DateTime.Now;
        }
        public void SaveProcessingInfo(int lineCount, bool hadValidLineEnding, bool isRealTime)
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            var timeElapsed = DateTime.Now - _startTime;
            stringBuilder.Append(DateTime.Now.ToString("M/d/yy h:mm:ss.fff") + $" {(isRealTime?"(REAL TIME) ":"")}Processed,{(isRealTime ? "" : " and")} saved{(isRealTime ? ", and updated state of" : "")} {lineCount.ToString("N2")} lines {(hadValidLineEnding ? "" : "with a straggler")} in {timeElapsed.TotalMilliseconds}ms");
            using (FileStream fs = new FileStream(_fileName, FileMode.Append, FileAccess.Write, FileShare.Read)) 
            using(StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine(stringBuilder.ToString());
            }
        }
        public void SaveStateUpdateInfo(int lineCount, bool isRealTime)
        {
            StringBuilder stringBuilder = new StringBuilder();

            var timeElapsed = DateTime.Now - _startTime;
            stringBuilder.Append(DateTime.Now.ToString("M/d/yy h:mm:ss.fff") + $" {(isRealTime ? "(REAL TIME) " : "")}Updated state from {lineCount.ToString("N2")} lines in {timeElapsed.TotalMilliseconds}ms");
            using (FileStream fs = new FileStream(_fileName, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (StreamWriter sw = new StreamWriter(fs))
            {
                sw.WriteLine(stringBuilder.ToString());
            }
        }
    }
}
