using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.DataStructures.EncounterInfo
{
    public static class EncounterLoader
    {
        public static string GetLeaderboardFriendlyDifficulty(string logLocationString)
        {
            var def = SupportedRaidDifficulties.FirstOrDefault(logLocationString.Contains);
            return def switch
            {
                "histoire" => "Story",
                "vétéran" => "Veteran",
                "maître" => "Master",
                _ => def
            };
        }

        public static string GetLeaderboardFriendlyPlayers(string logLocationString)
        {
            var def = SupportedNumberOfPlayers.FirstOrDefault(logLocationString.Contains);
            return def switch
            {
                "8\u00A0joueurs" => "8 Player",
                "16\u00A0joueurs" => "16 Player",
                _ => def
            };
        }
        public static List<EncounterInfo> SupportedEncounters = new List<EncounterInfo>();
        public static List<EncounterInfo> PVPEncounters = new List<EncounterInfo>();
        public static List<string> SupportedRaidDifficulties = new List<string>() { "Story", "Veteran", "Master","histoire","vétéran","maître" };
        public static List<string> SupportedNumberOfPlayers = new List<string>() { "8 Player", "16 Player","8\u00A0joueurs", "16\u00A0joueurs" };

        public static void LoadAllEncounters()
        {
            try
            {
                var raids = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/RaidNames.json"));
                LogIdFactory.AddIdsToEncounters(raids);
                var flashpoints = JsonConvert.DeserializeObject<List<EncounterInfo>>(File.ReadAllText(@"DataStructures/EncounterInfo/FlashpointInfo.json"));
                LogIdFactory.AddIdsToEncounters(flashpoints);
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
