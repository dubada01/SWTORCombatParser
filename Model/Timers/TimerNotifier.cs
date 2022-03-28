using SWTORCombatParser.DataStructures;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Timers
{
    public static class TimerNotifier
    {
        public static event Action<TimerInstanceViewModel> NewTimerTriggered = delegate { };
        public static event Action<TimerInstanceViewModel> TimerRefreshed = delegate { };
        public static void FireTimerTriggered(TimerInstanceViewModel timertriggered)
        {
            NewTimerTriggered(timertriggered);
        }
        public static void FireTimerRefreshed(TimerInstanceViewModel timerRefreshed)
        {
            TimerRefreshed(timerRefreshed);
        }
    }
}
