using System;
using System.Diagnostics;
using System.Linq;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.Model.CombatParsing;

public static class EncounterMonitor
{
    public static event Action<EncounterCombat> EncounterUpdated = delegate { };
    private static EncounterCombat _currentEncounterInfo;

    public static void FireEncounterUpdated()
    {
        EncounterUpdated?.InvokeSafely(_currentEncounterInfo);
    }

    public static EncounterCombat GetCurrentEncounter()
    {
        return _currentEncounterInfo;
    }
    public static void SetCurrentEncounter(EncounterCombat encounterInfo)
    {
        if (_currentEncounterInfo != null && encounterInfo.Combats.Any() && _currentEncounterInfo.Combats.First().StartTime == encounterInfo.Combats.First().StartTime)
            return;
        _currentEncounterInfo = encounterInfo;
        if(encounterInfo.Combats.Any())
            FireEncounterUpdated();
    }
}