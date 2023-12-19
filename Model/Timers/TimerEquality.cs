using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers
{
    public static class TimerEquality
    {
        public static bool Equals(Timer timer1, Timer timer2)
        {
            return
                (
                    timer1.Id == timer2.Id
                );

        }
    }
}
