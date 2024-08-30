using SWTORCombatParser.DataStructures.EncounterInfo;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.Utilities
{
    public static class EncounterLister
    {
        public static List<string> EncounterNames => GetEncounters();
        private static List<string> GetEncounters()
        {
            var encounters = EncounterLoader.SupportedEncounters;
            return encounters.Select(e => e.Name).ToList();
        }
        public static List<string> SortedEncounterNames => new List<string> { "All" }.Concat(GetSortedEncountersByType()).ToList();
        public static List<string> SortedEncounterNamesNoAll => GetSortedEncountersByType();
        public static List<EncounterInfo> SortedEncounterInfos => GetSortedEncounterInfos();
        private static List<EncounterInfo> GetSortedEncounterInfos()
        {
            var encounters = EncounterLoader.SupportedEncounters;
            var flashpoints = encounters.Where(e => e.EncounterType == EncounterType.Flashpoint).OrderBy(f => f.Name);
            var operations = encounters.Where(e => e.EncounterType == EncounterType.Operation).OrderBy(o => o.Name);
            var lairs = encounters.Where(e => e.EncounterType == EncounterType.Lair).OrderBy(l => l.Name);
            var listOfEncounters = new List<EncounterInfo>();
            listOfEncounters.Add(new EncounterInfo { Name = "--Operations--" });
            listOfEncounters.AddRange(operations);
            listOfEncounters.Add(new EncounterInfo { Name = "--Lairs--" });
            listOfEncounters.AddRange(lairs);
            listOfEncounters.Add(new EncounterInfo { Name = "--Flashpoints--" });
            listOfEncounters.AddRange(flashpoints);
            listOfEncounters.Add(new EncounterInfo { Name = "--Open World--"});
            listOfEncounters.Add(encounters.First(e => e.Name == "Open World"));
            return listOfEncounters;
        }
        private static List<string> GetSortedEncountersByType()
        {
            var encounters = EncounterLoader.SupportedEncounters;
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
            listOfEncounters.Add("--Open World--");
            listOfEncounters.Add(encounters.First(e => e.EncounterType == EncounterType.OpenWorld).Name);
            return listOfEncounters;
        }
        public static List<string> GetBossesForEncounter(string encounter)
        {
            var encounters = EncounterLoader.SupportedEncounters;
            var encounterSelected = encounters.FirstOrDefault(e => e.Name == encounter);
            if (encounterSelected == null)
                return new List<string>();
            return encounterSelected.BossIds.Keys.ToList();
        }
        public static List<string> GetAllTargetsForEncounter(string encounter)
        {
            var encounters = EncounterLoader.SupportedEncounters;
            var encounterSelected = encounters.FirstOrDefault(e => e.Name == encounter);
            return encounterSelected.BossNames.SelectMany(bn => bn.Contains("~?~") ? bn.Split("~?~")[1].Split('|').ToList() : new List<string> { bn }).ToList();
        }
        public static List<string> GetTargetsOfBossFight(string encounter, string bossFight)
        {
            var encounters = EncounterLoader.SupportedEncounters;
            var encounterSelected = encounters.FirstOrDefault(e => e.Name == encounter);
            var rawBossNamesForFight = encounterSelected.BossNames.FirstOrDefault(bn =>
                bn.Contains("~?~") ? bn.Split("~?~")[0] == bossFight : bn == bossFight);
            if(rawBossNamesForFight
                == null) return new List<string>();
            if (rawBossNamesForFight.Contains("~?~"))
            {
                return rawBossNamesForFight.Split("~?~")[1].Split("|").ToList();
            }
            return new List<string>() { rawBossNamesForFight };
        }
    }
}
