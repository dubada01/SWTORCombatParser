using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.DataStructures.Timers.HOT_Timers
{
    public static class DotTimerLoader
    {
        public static void TryLoadDots()
        {
            /*var currentHotTimers = DefaultTimersManager.GetDefaults("HOTS");
            if (currentHotTimers.Timers.Count > 0)
                return;*/
            //DefaultTimersManager.ResetTimersForSource("DOTS");
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"DataStructures/Timers/DOT Timers/timers.json")));
            var timers = (timerToLoad["Timers"] as JArray).ToObject<List<Timer>>();
            List<Timer> copiedTimers = new List<Timer>();
            foreach (var timer in timers)
            {
                timer.IsBuiltInDot = true;
                timer.ResetOnEffectLoss = true;
                timer.TrackOutsideOfCombat = false;
                timer.ShowIconIfPossible = true;
                copiedTimers.Add(timer.Copy());
            }
            DefaultOrbsTimersManager.AddTimersForSource(copiedTimers, "DOTS");
        }
    }
}
