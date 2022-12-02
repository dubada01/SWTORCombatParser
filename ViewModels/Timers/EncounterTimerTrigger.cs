using System;

namespace SWTORCombatParser.ViewModels.Timers
{
    public static class EncounterTimerTrigger
    {
        public static event Action<string,string,string> EncounterDetected = delegate { };
        public static event Action PvPEncounterEntered = delegate { };
        public static event Action NonPvpEncounterEntered = delegate { };
        public static event Action EncounterEnded = delegate { };
        public static void FireEncounterDetected(string encounterName, string bossName, string difficulty)
        {
            EncounterDetected(encounterName,bossName,difficulty);
        }
        public static void FirePvpEncounterDetected()
        {
            PvPEncounterEntered();
        }
        public static void FireNonPvpEncounterDetected()
        {
            NonPvpEncounterEntered();
        }
        public static void FireEnded()
        {
            EncounterEnded();
        }
    }
}
