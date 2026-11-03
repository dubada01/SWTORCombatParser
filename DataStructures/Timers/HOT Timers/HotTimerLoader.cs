using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.DataStructures.Timers.HOT_Timers
{
    public static class HotTimerLoader
    {
        public static void TryLoadHots()
        {
            //DefaultTimersManager.ResetTimersForSource("HOTS");
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(Path.Combine(Environment.CurrentDirectory, @"DataStructures/Timers/HOT Timers/timers.json")));
            var timers = (timerToLoad["Timers"] as JArray).ToObject<List<Timer>>();
            List<Timer> copiedTimers = new List<Timer>();
            foreach (var timer in timers)
            {
                timer.ResetOnEffectLoss = true;
                timer.TrackOutsideOfCombat = true;
                timer.IsHot = true;
                copiedTimers.Add(timer);

            }
            DefaultOrbsTimersManager.AddTimersForSource(copiedTimers, "HOTS");
        }
    }
}
