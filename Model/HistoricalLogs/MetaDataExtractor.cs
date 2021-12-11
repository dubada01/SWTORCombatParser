using MoreLinq;
using SWTORCombatParser.DataStructures.RaidInfos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.HistoricalLogs
{
    public static class MetaDataExtractor
    {
        public static List<Entity> GetLocalEntities(List<Combat> combats)
        {
            return combats.Select(c => c.LocalPlayer).DistinctBy(c=>c.Name).ToList();
        }
        public static List<EncounterInfo> GetEncountersForCharacter(List<Combat> combats, Entity character)
        {
            return combats.Where(comb => comb.LocalPlayer == character).Select(c => c.ParentEncounter).Distinct().ToList();
        }
        public static List<EncounterInfo> GetAllEncounters(List<Combat> combats)
        {
            return combats.Select(c => c.ParentEncounter).Distinct().ToList();
        }
        public static List<string> GetAllBossesFromEncounters(List<EncounterInfo> encounters)
        {
            return encounters.SelectMany(e => e.BossNames.Select(n=>n.Split("~?~")[0])).Distinct().ToList();
        }
    }
}
