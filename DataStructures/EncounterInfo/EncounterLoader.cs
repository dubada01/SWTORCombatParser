using Newtonsoft.Json;
using SWTORCombatParser.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SWTORCombatParser.DataStructures.EncounterInfo
{
    public static class EncounterLoader
    {
        public static string GetLeaderboardFriendlyDifficulty(ulong difficultyId)
        {
            switch (difficultyId)
            {
                case 836045448953651:
                case 836045448953653:
                case 836045448953658:
                    return "Story";
                case 836045448953652:
                case 836045448953654:
                case 836045448953657:
                    return "Veteran";
                case 836045448953655:
                case 836045448953656:
                case 836045448953659:
                    return "Master";
                default:
                    return "";
            }
        }

        public static string GetLeaderboardFriendlyPlayers(ulong difficultyId)
        {
            switch (difficultyId)
            {
                case 836045448953651:
                case 836045448953652:
                case 836045448953655:
                    return "8 Player";
                case 836045448953653:
                case 836045448953656:
                case 836045448953654:
                    return "16 Player";
                default:
                    return "";
            }
        }
        public static List<OpenWorldBoss> OpenWorldBosses = new List<OpenWorldBoss>();
        public static List<EncounterInfo> SupportedEncounters = new List<EncounterInfo>();
        public static List<EncounterInfo> PVPEncounters = new List<EncounterInfo>();

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
                var openWorldBosses = JsonConvert.DeserializeObject<List<OpenWorldBoss>>(File.ReadAllText(@"DataStructures/EncounterInfo/OpenWorldBosses.json"));

                OpenWorldBosses = openWorldBosses;
                SupportedEncounters = pvpMatches;
                SupportedEncounters.AddRange(raids);
                SupportedEncounters.AddRange(flashpoints);
                SupportedEncounters.Add(new EncounterInfo
                {
                    EncounterType = EncounterType.OpenWorld,
                    Name = "Open World",
                    LogName = "Open World",
                    BossNames = OpenWorldBosses.Select(owb => owb.BossName).ToList(),
                    BossIds = OpenWorldBosses.ToDictionary(
    kvp => kvp.BossName,
    kvp => new Dictionary<string, List<long>>
    {
                        { "Open World", new List<long> { kvp.BossId } }
    }),
                    BossInfos = OpenWorldBosses.Select(
                        owb => new BossInfo {
                            EncounterName ="Open World", 
                            IsOpenWorld = true, 
                            TargetIds = OpenWorldBosses.Select(owbId=>owbId.BossId).ToList(),
                            TargetsRequiredForKill = OpenWorldBosses.Select(owbId => owbId.BossId).ToList()
                        }).ToList(),    
                });
            }
            catch (Exception e)
            {
                Logging.LogError("Failed to load encounter infos:" + e.Message);
            }
        }
    }
}
