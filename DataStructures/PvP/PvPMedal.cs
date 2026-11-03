using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.DataStructures.PvP;


public enum MedalType
{
    DPS,
    Healer,
    Tank,
    General
}

public enum MedalLogic
{
    TotalDamage,
    TotalHealing,
    TotalProtection,
    MaxDamage,
    MaxHeal,
    KillCount
}

public class PvPMedal
{
    public EncounterType EncounterType;
    public MedalType MedalType;
    public MedalLogic MedalLogic;
    public string MedalName;
    public string ThresholdUnit;
    public int MedalThreshold;
}

public static class PvPMedalLoader
{
    
    public static List<PvPMedal> PvPMedals = new();
    public static void Init()
    {
        PvPMedals = JsonConvert.DeserializeObject<List<PvPMedal>>(File.ReadAllText(@"DataStructures/PvP/pvp_medals.json")) ?? new  List<PvPMedal>();
    }

}