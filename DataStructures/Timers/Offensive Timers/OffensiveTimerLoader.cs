using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SWTORCombatParser.DataStructures.Timers.Offensive_Timers
{
    public class OffensiveTimerLoader
    {
        public static void TryLoadOffensives()
        {
            var timerToLoad = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(@".\DataStructures\Timers\Offensive Timers\timers.json"));
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
            DefaultTimersManager.AddTimersForSource(copiedTimers, "OCD");
        }
    }
}
