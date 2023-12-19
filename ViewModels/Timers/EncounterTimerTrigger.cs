using System;

namespace SWTORCombatParser.ViewModels.Timers
{
    public static class EncounterTimerTrigger
    {
        public static (string, string, string) CurrentEncounter { get; set; } = ("", "", "");
        public static event Action<string, string, string> EncounterDetected = delegate { };
        public static event Action PvPEncounterEntered = delegate { };
        public static event Action NonPvpEncounterEntered = delegate { };
        public static void FireEncounterDetected(string encounterName, string bossName, string difficulty)
        {
            if (CurrentEncounter.Item1 == encounterName && CurrentEncounter.Item2 == bossName && CurrentEncounter.Item3 == difficulty)
                return;
            CurrentEncounter = (encounterName, bossName, difficulty);
            EncounterDetected(encounterName, bossName, difficulty);
        }
        public static void FirePvpEncounterDetected()
        {
            PvPEncounterEntered();
        }
        public static void FireNonPvpEncounterDetected()
        {
            NonPvpEncounterEntered();
        }
    }
}
