using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SWTORCombatParser.DataStructures.Timers.Boss_Timers
{
    public static class BossTimerLoader
    {
        public static void TryLoadBossTimers()
        {
            List<DefaultTimersData> bossTimerData = new List<DefaultTimersData>();
            foreach (var file in Directory.EnumerateFiles(Path.Combine(Environment.CurrentDirectory, @"DataStructures/Timers/Boss Timers/Raids"), "*", SearchOption.AllDirectories))
            {
                var bossTimers = JsonConvert.DeserializeObject<JArray>(File.ReadAllText(file));
                if (bossTimers == null)
                    continue;
                var bossTimerDeserialized = bossTimers.ToObject<List<DefaultTimersData>>();
                bossTimerData.AddRange(bossTimerDeserialized);
            }

            var currentRev = bossTimerData
                .Where(b => b.Timers.Any())
                .Select(b => b.Timers.Max(t => t.TimerRev))
                .DefaultIfEmpty(0)
                .Max();

            DefaultOrbsTimersManager.ClearBuiltinMechanics(currentRev);
            var currentBossTimers = DefaultOrbsTimersManager.GetAllDefaults();
            currentBossTimers.ToList().RemoveAll(t => t.Timers.Any(timer => timer.SpecificBoss == "Operations Training Dummy"));

            var sourcesToAdd = new List<DefaultTimersData>();
            foreach (var source in bossTimerData)
            {
                if (source.Timers.Count == 0)
                    continue;
                source.IsBossSource = true;
                foreach (var timer in source.Timers)
                {
                    if (timer.TriggerType == TimerKeyType.EntityHP)
                    {
                        timer.TargetIsLocal = timer.SourceIsLocal;
                        timer.Source = "Any";
                        timer.SourceIsLocal = false;
                    }

                    timer.ShowIconIfPossible = true;
                    timer.IsMechanic = true;
                    timer.TimerRev = currentRev;
                }
                sourcesToAdd.Add(source);
            }
            DefaultOrbsTimersManager.AddSources(sourcesToAdd);
        }
    }
}
