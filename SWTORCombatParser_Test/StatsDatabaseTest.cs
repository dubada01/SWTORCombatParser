using NUnit.Framework;
using SWTORCombatParser.Model.CloudRaiding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser_Test
{
    [TestFixture]
    public class StatsDatabaseTest
    {
        [Test]
        public void TestStatsDatabase()
        {
            Stats.RecordCombatState(new SWTORCombatParser.DataStructures.Combat {
                EncounterBossDifficultyParts = ("TYTH","8","Master"), 
                DurationOverride = 10000000, 
                ParentEncounter = new SWTORCombatParser.DataStructures.EncounterInfo.EncounterInfo { Name = "Gods From The Machine"},
                StartTime = DateTime.Now,
            BossInfo = new SWTORCombatParser.DataStructures.EncounterInfo.BossInfo(),
            BossKillOverride = true}).Wait();
        }
    }
}
