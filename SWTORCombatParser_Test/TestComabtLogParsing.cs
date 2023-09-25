using NUnit.Framework;
using SWTORCombatParser.Model.LogParsing;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SWTORCombatParser_Test
{

    public class TestComabtLogParsing
    {
        private string _logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"Star Wars - The Old Republic\CombatLogs");
        [Test]
        public void TestParseLog()
        {
            var logName = "combat_2021-07-11_19_01_39_463431.txt";
            var testLogPath = "testLog.txt";
            File.Create(testLogPath).Close();


            TransferLogData(logName, testLogPath);
            //test that the log is parsed correctly
            var log = CombatLogLoader.LoadSpecificLog(testLogPath);
            if (log != null)
                Assert.Pass();
            else
                Assert.Fail();
        }

        private void TransferLogData(string logName, string testLogPath)
        {
            var logLines = File.ReadAllLines(Path.Combine(_logPath, logName), new UTF7Encoding());
            using (var fs = new FileStream(testLogPath, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                var logIndex = 0;
                while (logIndex < logLines.Length)
                {
                    var logsToMove = Math.Min(logLines.Length - logIndex, new Random().Next(1, 30));
                    for (var i = 0; i < logsToMove; i++)
                    {
                        var stringBytes = new UTF7Encoding(true).GetBytes(logLines[logIndex + i] + '\n');
                        fs.Write(stringBytes);
                        fs.Flush();
                    }
                    logIndex += logsToMove;
                    Thread.Sleep(5);
                }
            }
        }
    }
}
