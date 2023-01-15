using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SWTORCombatParser.DataStructures.Boss_Timers
{
    public static class BossTimerLoader
    {
        public static void TryLoadBossTimers()
        {
            var currentBossTimers = DefaultTimersManager.GetAllDefaults();
            currentBossTimers.ToList().RemoveAll(t => t.Timers.Any(timer => timer.SpecificBoss == "Operations Training Dummy"));
            List<DefaultTimersData> bossTimerData = new List<DefaultTimersData>();
            foreach (var file in Directory.EnumerateFiles(@".\DataStructures\Timers\Boss Timers\Raids","*",SearchOption.AllDirectories))
            {
                var bossTimers = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(file));
                if (bossTimers == null)
                    continue;
                bossTimerData.AddRange(bossTimers.ToObject<List<DefaultTimersData>>());
            }
            
            foreach (var source in bossTimerData)
            {
                if (source.Timers.Count == 0)
                    continue;
                source.IsBossSource = true;
                foreach (var timer in source.Timers)
                {
                    timer.Id = Guid.NewGuid().ToString();
                    if(timer.TriggerType == TimerKeyType.EntityHP)
                    {
                        timer.Target = timer.SpecificBoss;
                        timer.TargetIsLocal = timer.SourceIsLocal;
                        timer.Source = "Any";
                        timer.SourceIsLocal = false;
                    }
                    timer.IsMechanic = true;
                    timer.IsBuiltInMechanic = true;
                }
                DefaultTimersManager.AddSource(source);
            }
        }
    }
}
