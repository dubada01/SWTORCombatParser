using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SWTORCombatParser.DataStructures.Timers.Defensive_Timers
{
    public  class DefensiveTimerLoader
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
            DefaultTimersManager.AddTimersForSource(copiedTimers, "DCD");
        }
    }
}
