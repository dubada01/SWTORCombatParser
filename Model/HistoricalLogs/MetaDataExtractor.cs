//using MoreLinq;

using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Model.HistoricalLogs
{
    public static class MetaDataExtractor
    {
        public static List<Entity> GetLocalEntities(List<Combat> combats)
        {
            return combats.Select(c => c.LocalPlayer).DistinctBy(c => c.Name).ToList();
        }
        public static List<Entity> GetPlayersForBossInEncounter(List<Combat> combats, EncounterInfo encounter, string bossName)
        {
            return combats.Where(c => c.ParentEncounter.NamePlus == encounter.NamePlus && c.EncounterBossDifficultyParts.Item1 == bossName).Select(c => c.LocalPlayer).Distinct().ToList();
        }
        public static List<EncounterInfo> GetEncountersForCharacter(List<Combat> combats, Entity character)
        {
            return combats.Where(comb => comb.LocalPlayer == character).Select(c => c.ParentEncounter).Distinct().ToList();
        }
        public static List<EncounterInfo> GetAllEncounters(List<Combat> combats)
        {
            return combats.Select(c => c.ParentEncounter).DistinctBy(e => e.NamePlus).ToList();
        }
        public static List<string> GetAllBossesFromCombats(List<Combat> combats)
        {
            return combats.Where(c => c.IsCombatWithBoss).Select(e => e.EncounterBossDifficultyParts.Item1).Distinct().ToList();
        }
    }
}
