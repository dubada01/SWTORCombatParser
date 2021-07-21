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
            var specificLog = CombatLogLoader.LoadSpecificLog(Path.Combine(_logPath
                , logName));
            var parsedLog = CombatLogParser.ParseAllLines(specificLog);
        }
        [Test]
        public void TestIdentifyCombat()
        {
            var logName = "combat_2021-07-11_15_39_07_966187.txt";
            var specificLog = CombatLogLoader.LoadSpecificLog(Path.Combine(_logPath
                , logName));
            var parsedLog = CombatLogParser.ParseAllLines(specificLog);
            var combats = CombatIdentifier.GetActiveCombatLogs(parsedLog);


        }
        [Test]
        public void TestGetMostRecentCombat()
        {
            var recentCombats = CombatLogLoader.GetMostRecentCombats();
        }
    }
}
