using SWTORCombatParser.DataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.Timers
{
    public static class TimerEquality
    {
        public static bool Equals(Timer timer1, Timer timer2)
        {
            return
                (
                timer1.Name == timer2.Name &&
                timer1.Source == timer2.Source &&
                timer1.Target == timer2.Target &&
                timer1.HPPercentage == timer2.HPPercentage &&
                timer1.TriggerType == timer2.TriggerType &&
                timer1.Ability == timer2.Ability &&
                timer1.Effect == timer2.Effect &&
                timer1.IsPeriodic == timer2.IsPeriodic &&
                timer1.IsAlert == timer2.IsAlert &&
                timer1.DurationSec == timer2.DurationSec &&
                timer1.TimerColor == timer2.TimerColor &&
                timer1.SpecificBoss == timer2.SpecificBoss &&
                timer1.SpecificEncounter == timer2.SpecificEncounter &&
                timer1.SpecificDifficulty == timer2.SpecificDifficulty
                );
                
        }
    }
}
