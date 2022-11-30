using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.DataStructures.EncounterInfo
{
    public static class EncounterLoader
    {
        public static List<EncounterInfo> SupportedEncounters = new List<EncounterInfo>();
        public static List<EncounterInfo> PVPEncounters = new List<EncounterInfo>();
        public static List<string> SupportedRaidDifficulties = new List<string>() { "Story", "Veteran", "Master" };
        public static List<string> SupportedNumberOfPlayers = new List<string>() { "8 Player", "16 Player" };

        public static void LoadAllEncounters()
        {
            try
            {
                var raids = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/RaidNames.json"));
                var flashpoints = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/FlashpointInfo.json"));
                var pvpMatches = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/PVPAreaNames.json"));
                foreach (var match in pvpMatches)
                {
                    match.LogName = match.Name;
                }
                SupportedEncounters = pvpMatches;
                SupportedEncounters.AddRange(raids);
                SupportedEncounters.AddRange(flashpoints);
            }
            catch(Exception e)
            {
                Logging.LogError("Failed to load encounter infos:"+e.Message);
            }
        }
    }
}
