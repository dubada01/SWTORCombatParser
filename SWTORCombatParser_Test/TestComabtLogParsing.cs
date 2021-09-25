using NUnit.Framework;
using SWTORCombatParser;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser_Test
{
    
    public class TestComabtLogParsing
    {
        private string _logPath = @"C:\Users\duban\Documents\Star Wars - The Old Republic\CombatLogs";
        [Test]
        public void TestParseLog()
        {
            var logName = "combat_2020-06-12_13_11_42_685105.txt";
            var testLogPath = "testLog.txt";
            File.Create(testLogPath);

            var streamer = new CombatLogStreamer();
            streamer.MonitorLog(testLogPath);

            TransferLogData(logName, testLogPath);
            
        }

        private void TransferLogData(string logName, string testLogPath)
        {
            var logLines = File.ReadAllLines(logName);
            var logIndex = 0;
            while(logIndex < logLines.Length)
            {
                var logsToMove = Math.Min(logLines.Length - logIndex, new Random().Next(1, 15));
                for(var i = 0; i < logsToMove; i++)
                {
                    File.
                }
            }
        }
    }
}
