using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.DataStructures.Timers.Offensive_Timers
{
    public class OffensiveTimerLoader
    {
        public static void TryLoadOffensives()
        {
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText( Path.Combine(Environment.CurrentDirectory, @"DataStructures/Timers/Offensive Timers/timers.json")));
            var timers = (timerToLoad["Timers"] as JArray).ToObject<List<Timer>>();
            List<Timer> copiedTimers = new List<Timer>();
            foreach (var timer in timers)
            {
                timer.IsBuiltInOffensive = true;
                timer.ResetOnEffectLoss = true;
                timer.TrackOutsideOfCombat = true;
                timer.Source = "LocalPlayer";
                timer.Target = "LocalPlayer";
                copiedTimers.Add(timer.Copy());
            }
            DefaultOrbsTimersManager.AddTimersForSource(copiedTimers, "OCD");
        }
    }
}
