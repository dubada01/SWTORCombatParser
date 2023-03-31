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
            List<DefaultTimersData> bossTimerData = new List<DefaultTimersData>();
            foreach (var file in Directory.EnumerateFiles(@".\DataStructures\Timers\Boss Timers\Raids", "*", SearchOption.AllDirectories))
            {
                var bossTimers = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(file));
                if (bossTimers == null)
                    continue;
                var bossTimerDeserialized = bossTimers.ToObject<List<DefaultTimersData>>();
                bossTimerData.AddRange(bossTimerDeserialized);
            }

            var currentRev = bossTimerData.Any() ? bossTimerData.First().Timers.First().TimerRev : 0;

            DefaultTimersManager.ClearBuiltinMechanics(currentRev);
            var currentBossTimers = DefaultTimersManager.GetAllDefaults();
            currentBossTimers.ToList().RemoveAll(t => t.Timers.Any(timer => timer.SpecificBoss == "Operations Training Dummy"));

            
            foreach (var source in bossTimerData)
            {
                if (source.Timers.Count == 0)
                    continue;
                source.IsBossSource = true;
                foreach (var timer in source.Timers)
                {
                    if(timer.TriggerType == TimerKeyType.EntityHP)
                    {
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
