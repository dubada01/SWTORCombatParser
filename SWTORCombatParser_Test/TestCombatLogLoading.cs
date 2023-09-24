using NUnit.Framework;
using SWTORCombatParser.Model.LogParsing;

namespace SWTORCombatParser_Test
{
    public class TestCombatLogLoading
    {

        [Test]
        public void TestLoadCombatLogs()
        {
            var allCombatLogs = CombatLogLoader.LoadAllCombatLogs();
            if (allCombatLogs.Length > 0 && allCombatLogs[0].Time > allCombatLogs[allCombatLogs.Length - 1].Time)
                Assert.Pass();
            else
                Assert.Fail();
        }
        [Test]
        public void TestLoadMostRecentLog()
        {
            var allCombatLogs = CombatLogLoader.LoadAllCombatLogs();
            var mostRecentLog = CombatLogLoader.LoadMostRecentLog();
            if (mostRecentLog != null && mostRecentLog.Time == allCombatLogs[0].Time)
                Assert.Pass();
            else
                Assert.Fail();
        }
        [Test]
        public void TestLoadSpecifcLog()
        {
            var logName = "combat_2021-07-11_15_39_07_966187.txt";
            var specificLog = CombatLogLoader.LoadSpecificLog(logName);
            if (specificLog != null)
                Assert.Pass();
        }

    }
}