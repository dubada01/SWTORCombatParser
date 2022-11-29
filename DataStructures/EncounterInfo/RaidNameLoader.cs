using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace SWTORCombatParser.DataStructures.EncounterInfo
{
    public static class RaidNameLoader
    {
        public static List<EncounterInfo> SupportedEncounters = new List<EncounterInfo>();
        public static List<string> SupportedRaidDifficulties = new List<string>() { "Story", "Veteran", "Master" };
        public static List<string> SupportedNumberOfPlayers = new List<string>() { "8 Player", "16 Player" };

        public static void LoadAllRaidNames()
        {
            try
            {
                var raids = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/RaidNames.json"));
                var flashpoints = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/FlashpointInfo.json"));
                SupportedEncounters.AddRange(raids);
                SupportedEncounters.AddRange(flashpoints);
            }
            catch(Exception e)
            {

            }
        }
    }
}
