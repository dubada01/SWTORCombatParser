using System;

namespace SWTORCombatParser.ViewModels.Timers
{
    public static class EncounterTimerTrigger
    {
        public static event Action<string,string,string> EncounterDetected = delegate { };
        public static event Action EncounterEnded = delegate { };
        public static void FireEncounterDetected(string encounterName, string bossName, string difficulty)
        {
            EncounterDetected(encounterName,bossName,difficulty);
        }
        public static void FireEnded()
        {
            EncounterEnded();
        }
    }
}
