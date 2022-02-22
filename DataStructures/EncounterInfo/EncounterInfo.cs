using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.DataStructures.RaidInfos
{
    public enum EncounterType
    {
        Flashpoint,
        Operation,
        Lair
    }
    public class BossInfo
    {
        public string EncounterName { get; set; }
        public List<string> TargetNames { get; set; }
    }
    public class EncounterInfo
    {
        public static EncounterInfo GetCopy(EncounterInfo source)
        {
            return new EncounterInfo
            {
                LogName =  source.LogName,
                Name = source.Name,
                BossInfos = source.BossInfos,
                BossNames = source.BossNames
            };
        }
        public string Difficutly { get; set; } = "";
        public string NumberOfPlayer { get; set; } = "";
        private List<string> bossNames;
        public EncounterType EncounterType { get; set; }
        public string LogName { get; set; }
        public string Name { get; set; }
        public List<string> BossNames { get => bossNames; set
            {
                bossNames = value;
                BossInfos = BossNames.Select(b => new BossInfo() { EncounterName = b.Contains("~?~") ? b.Split("~?~", StringSplitOptions.None)[0] : b, TargetNames = b.Contains("~?~") ? b.Split("~?~", StringSplitOptions.None)[1].Split('|').ToList() : new List<string>() { b } }).ToList();
            } 
        }
        public bool IsBossEncounter => BossInfos != null;
        public List<BossInfo> BossInfos { get; set; } 

    }
}
