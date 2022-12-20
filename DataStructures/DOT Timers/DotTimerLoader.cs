﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.DataStructures.HOT_Timers
{
    public static class DotTimerLoader
    {
        public static void TryLoadDots()
        {
            /*var currentHotTimers = DefaultTimersManager.GetDefaults("HOTS");
            if (currentHotTimers.Timers.Count > 0)
                return;*/
            DefaultTimersManager.ResetTimersForSource("HOTS");
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@".\DataStructures\DOT Timers\timers.json"));
            var timers = (timerToLoad["Timers"] as JArray).ToObject<List<Timer>>();
            foreach(var timer in timers)
            {
                DefaultTimersManager.AddTimerForSource(timer, "HOTS");
            }
        }
    }
}