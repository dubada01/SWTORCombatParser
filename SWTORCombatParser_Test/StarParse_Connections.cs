using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            DefaultTimersManager.AddTimersForSource(timers, "StarParse Import");
        }
    }
}
