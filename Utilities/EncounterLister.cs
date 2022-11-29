using System.Collections.Generic;
using System.Linq;
using SWTORCombatParser.DataStructures.EncounterInfo;

namespace SWTORCombatParser.Utilities
{
    public static class EncounterLister
    {
        public static List<string> EncounterNames => GetEncounters();
        private static List<string> GetEncounters()
        {
            var encounters = RaidNameLoader.SupportedEncounters;
            return encounters.Select(e => e.Name).ToList();
        }
        public static List<string> SortedEncounterNames => new List<string> { "All"}.Concat(GetSortedEncountersByType()).ToList();
        public static List<string> SortedEncounterNamesNoAll => GetSortedEncountersByType();
        public static List<EncounterInfo> SortedEncounterInfos => GetSortedEncounterInfos();
        private static List<EncounterInfo> GetSortedEncounterInfos()
        {
            var encounters = RaidNameLoader.SupportedEncounters;
            var flashpoints = encounters.Where(e => e.EncounterType == EncounterType.Flashpoint).OrderBy(f => f.Name);
            var operations = encounters.Where(e => e.EncounterType == EncounterType.Operation).OrderBy(o => o.Name);
            var lairs = encounters.Where(e => e.EncounterType == EncounterType.Lair).OrderBy(l => l.Name);
            var listOfEncounters = new List<EncounterInfo>();
            listOfEncounters.Add(new EncounterInfo { Name ="--Operations--" });
            listOfEncounters.AddRange(operations);
            listOfEncounters.Add(new EncounterInfo { Name ="--Lairs--" });
            listOfEncounters.AddRange(lairs);
            listOfEncounters.Add(new EncounterInfo { Name ="--Flashpoints--" });
            listOfEncounters.AddRange(flashpoints);
            return listOfEncounters;
        }
        private static List<string> GetSortedEncountersByType()
        {
            var encounters = RaidNameLoader.SupportedEncounters;
            var flashpoints = encounters.Where(e => e.EncounterType == EncounterType.Flashpoint).OrderBy(f => f.Name);
            var flashpointNames = flashpoints.Select(f => f.Name);
            var operations = encounters.Where(e => e.EncounterType == EncounterType.Operation).OrderBy(o => o.Name);
            var operationNames = operations.Select(o => o.Name);
            var lairs = encounters.Where(e => e.EncounterType == EncounterType.Lair).OrderBy(l => l.Name);
            var lairNames = lairs.Select(l => l.Name);
            var listOfEncounters = new List<string>();
            listOfEncounters.Add("--Operations--");
            listOfEncounters.AddRange(operationNames);
            listOfEncounters.Add("--Lairs--");
            listOfEncounters.AddRange(lairNames);
            listOfEncounters.Add("--Flashpoints--");
            listOfEncounters.AddRange(flashpointNames);
            return listOfEncounters;
        }
        public static List<string> GetBossesForEncounter(string encounter)
        {
            var encounters = RaidNameLoader.SupportedEncounters;
            var encounterSelected = encounters.FirstOrDefault(e => e.Name == encounter);
            if (encounterSelected == null)
                return new List<string>();
            return encounterSelected.BossInfos.Select(bi=>bi.EncounterName).ToList();
        }
        public static List<string> GetAllTargetsForEncounter(string encounter)
        {
            var encounters = RaidNameLoader.SupportedEncounters;
            var encounterSelected = encounters.FirstOrDefault(e => e.Name == encounter);
            return encounterSelected.BossInfos.SelectMany(bi=>bi.TargetNames).ToList();
        }
        public static List<string> GetTargetsOfBossFight(string encounter, string bossFight)
        {
            var encounters = RaidNameLoader.SupportedEncounters;
            var encounterSelected = encounters.FirstOrDefault(e => e.Name == encounter);
            var bossOfEncouter = encounterSelected.BossInfos.FirstOrDefault(bi => bi.EncounterName == bossFight);
            if (bossOfEncouter == null)
                return new List<string>();
            return bossOfEncouter.TargetNames;
        }
    }
}
