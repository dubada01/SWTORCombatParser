using System;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Timers
{
    public static class EncounterTimerTrigger
    {
        public static (string, string, string) CurrentEncounter { get; set; } = ("", "", "");
        public static event Action<string, string, string> BossCombatDetected = delegate { };
        public static event Action<DateTime, EncounterInfo> PvPEncounterEntered = delegate { };
        public static bool CurrentEncounterIsPVP = false;
        public static event Action<DateTime, EncounterInfo> NonPvpEncounterEntered = delegate { };
        public static void FireBossCombatDetected(string encounterName, string bossName, string difficulty, bool isRealtime)
        {
            if (CurrentEncounter.Item1 == encounterName && CurrentEncounter.Item2 == bossName && CurrentEncounter.Item3 == difficulty)
                return;
            CurrentEncounter = (encounterName, bossName, difficulty);
            if(isRealtime)
                BossCombatDetected.InvokeSafely(encounterName, bossName, difficulty);
        }
        public static void FirePvpEncounterDetected(DateTime changedTime, EncounterInfo encounterInfo)
        {
            PvPEncounterEntered.InvokeSafely(changedTime, encounterInfo);
            CurrentEncounterIsPVP = true;
        }
        public static void FireNonPvpEncounterDetected(DateTime changedTime, EncounterInfo encounterInfo)
        {
            NonPvpEncounterEntered.InvokeSafely(changedTime,encounterInfo);
            CurrentEncounterIsPVP = false;
        }

        public static void SetPvpStateAfterHistorical(DateTime timeAfterHistory)
        {
            var currentEncounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(timeAfterHistory);
            if(currentEncounter.IsPvpEncounter)
                FirePvpEncounterDetected(timeAfterHistory,currentEncounter);
            else
                FireNonPvpEncounterDetected(timeAfterHistory,currentEncounter);
        }
    }
}
