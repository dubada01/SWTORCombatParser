using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoreLinq;
using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;

namespace SWTORCombatParser_Test
{
    public class Test_AddBuiltinTimers
    {
        private int _currentRev = 1;
        [Test]
        public void AddKetsumesTimers()
        {
            var allTimers = JsonConvert.DeserializeObject<List<DefaultTimersData>>(
                File.ReadAllText(Path.Combine(Environment.CurrentDirectory,@"keetsuneTimers.json")));
            var allIndividualTimers = allTimers.SelectMany(t => t.Timers);
            var enumerable = allIndividualTimers as Timer[] ?? allIndividualTimers.ToArray();
            enumerable.ForEach(t=>
            {
                t.TimerSource = t.TimerSource.Count(t => t == '|') > 1 ? t.TimerSource.Split('|')[0] + "|" + t.TimerSource.Split('|')[1] : t.TimerSource;
                t.Id = Guid.NewGuid().ToString();
                t.TimerRev = _currentRev;
            });
            var timersWithAudio = enumerable.Where(t => !string.IsNullOrEmpty(t.CustomAudioPath));
            foreach (var timer in timersWithAudio)
            {
                timer.CustomAudioPath = timer.CustomAudioPath.Split('\\').Last();
            }

            foreach (var encounter in allTimers)
            {
                encounter.Timers.RemoveAll(timer =>
                    timer.Name == "Missle Salvo" || timer.Name == "Red Circles" || timer.Name == "Knock-back" ||
                    timer.Name == "Platform Drop");
            }
            
            var encounters = allTimers.Where(v=>v.IsBossSource).GroupBy(v => v.TimerSource.Split('|').First());
            var targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "Builtin Timers");
            Directory.CreateDirectory(targetDirectory);
            foreach (var encounter in encounters)
            {
                Directory.CreateDirectory(Path.Combine(targetDirectory, encounter.Key));
                File.WriteAllText(Path.Combine(targetDirectory,encounter.Key,encounter.Key)+".json",JsonConvert.SerializeObject(encounter.ToList()));
            }
        }
    }
}
