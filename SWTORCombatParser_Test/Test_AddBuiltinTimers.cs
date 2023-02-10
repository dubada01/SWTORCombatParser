using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SWTORCombatParser.Model.Timers;

namespace SWTORCombatParser_Test
{
    public class Test_AddBuiltinTimers
    {
        [Test]
        public void AddKetsumesTimers()
        {
            var allTimers = JsonConvert.DeserializeObject<List<DefaultTimersData>>(
                File.ReadAllText(@"C:\Users\duban\AppData\Local\DubaTech\SWTORCombatParser\timers_info_v3.json"));
            var timersWithAudio = allTimers.SelectMany(t => t.Timers).Where(t => !string.IsNullOrEmpty(t.CustomAudioPath));
            foreach (var timer in timersWithAudio)
            {
                timer.CustomAudioPath = timer.CustomAudioPath.Split('\\').Last();
            }
            var encounters = allTimers.Where(v=>v.IsBossSource).GroupBy(v => v.TimerSource.Split('|').First());
            var targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Builtin Timers");
            Directory.CreateDirectory(targetDirectory);
            foreach (var encounter in encounters)
            {
                File.WriteAllText(Path.Combine(targetDirectory,encounter.Key)+".json",JsonConvert.SerializeObject(encounter.ToList()));
            }
        }
    }
}
