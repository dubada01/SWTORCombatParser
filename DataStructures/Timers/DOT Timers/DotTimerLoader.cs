using Newtonsoft.Json;
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
            //DefaultTimersManager.ResetTimersForSource("DOTS");
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@".\DataStructures\Timers\DOT Timers\timers.json"));
            var timers = (timerToLoad["Timers"] as JArray).ToObject<List<Timer>>();
            List<Timer> copiedTimers = new List<Timer>();
            foreach (var timer in timers)
            {
                timer.IsBuiltInDot = true;
                timer.ResetOnEffectLoss = true;
                timer.TrackOutsideOfCombat = false;
                copiedTimers.Add(timer.Copy());
            }
            DefaultTimersManager.AddTimersForSource(copiedTimers, "DOTS");
        }
    }
}
