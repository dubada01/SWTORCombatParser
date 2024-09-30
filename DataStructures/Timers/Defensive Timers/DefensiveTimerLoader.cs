using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SWTORCombatParser.Model.Timers;
using System.Collections.Generic;
using System.IO;

namespace SWTORCombatParser.DataStructures.Timers.Defensive_Timers
{
    public class DefensiveTimerLoader
    {
        public static void TryLoadDefensives()
        {
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@".\DataStructures\Timers\Defensive Timers\timers.json"));
            var timers = (timerToLoad["Timers"] as JArray).ToObject<List<Timer>>();
            List<Timer> copiedTimers = new List<Timer>();
            foreach (var timer in timers)
            {

                timer.IsBuiltInDefensive = true;
                timer.ResetOnEffectLoss = true;
                timer.TrackOutsideOfCombat = true;
                timer.Source = "Any";
                timer.Target = "Any";
                copiedTimers.Add(timer.Copy());
            }
            DefaultOrbsTimersManager.AddTimersForSource(copiedTimers, "DCD");
        }
    }
}
