using NUnit.Framework;
using SWTORCombatParser.Model.Timers;
using System.IO;

namespace SWTORCombatParser_Test
{
    [TestFixture]
    public class StarParse_Connections
    {

        [Test]
        public void CheckTimerConversion()
        {
            var timers = ImportSPTimers.ConvertXML(File.ReadAllText(@"C:\Users\duban\AppData\Local\StarParse\app\client\app\starparse-timers.xml"));
            DefaultOrbsTimersManager.AddTimersForSource(timers, "StarParse Import");
        }
    }
}
