using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.DataStructures.Boss_Timers
{
    public static class BossTimerLoader
    {
        public static void TryLoadBossTimers()
        {
            var currentBossTimers = DefaultTimersManager.GetAllDefaults();
            currentBossTimers.ToList().RemoveAll(t => t.Timers.Any(timer => timer.SpecificBoss == "Operations Training Dummy"));
            if (currentBossTimers.Any(t => t.IsBossSource))
                return;
            var bossTimers = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(@".\DataStructures\Boss Timers\BossTimers.json"));
            //var devTimers = JsonConvert.DeserializeObject<DefaultTimersData>(File.ReadAllText(@".\DataStructures\Boss Timers\DevTimers.json"));
            var bosses = bossTimers.ToObject<List<DefaultTimersData>>();
            //bosses.Add(devTimers);
            foreach (var source in bosses)
            {
                if (source.Timers.Count == 0)
                    continue;
                source.IsBossSource = true;
                foreach (var timer in source.Timers)
                { 
                    if(timer.TriggerType == TimerKeyType.EntityHP)
                    {
                        timer.Target = timer.Source;
                        timer.TargetIsLocal = timer.SourceIsLocal;
                        timer.Source = "Any";
                        timer.SourceIsLocal = false;
                    }
                    timer.IsMechanic = true; 
                }
                DefaultTimersManager.AddSource(source);
            }
        }
    }
}
