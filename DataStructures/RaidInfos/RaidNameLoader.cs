using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SWTORCombatParser.DataStructures.RaidInfos
{
    public static class RaidNameLoader
    {
        public static List<RaidInfo> SupportedRaids = new List<RaidInfo>();
        public static List<string> SupportedRaidDifficulties = new List<string>() { "Story", "Veteran", "Master" };
        public static List<string> SupportedNumberOfPlayers = new List<string>() { "8 Player", "16 Player" };

        public static void LoadAllRaidNames()
        {
            try
            {
                SupportedRaids = JsonConvert.DeserializeObject<List<RaidInfo>>(File.ReadAllText(@"DataStructures/RaidInfos/RaidNames.json"));
            }
            catch(Exception e)
            {

            }
        }
    }
}
