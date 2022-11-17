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
        Lair,
        Parsing
    }
    public class BossInfo
    {
        public string EncounterName { get; set; }
        public List<string> TargetNames { get; set; } = new List<string>();
        public List<string> TargetsRequiredForKill { get; set; } = new List<string>();
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
        public string Difficutly { get; set; } = "Story";
        public string NumberOfPlayer { get; set; } = "4";
        private List<string> bossNames;
        public EncounterType EncounterType { get; set; }
        public string LogName { get; set; }
        public string NamePlus => Name + $" {{{NumberOfPlayer} {Difficutly}}}";
        public string Name { get; set; }
        public List<string> BossNames { get => bossNames; set
            {
                bossNames = value;
                BossInfos = BossNames.Select(b => new BossInfo() 
                { 
                    EncounterName = b.Contains("~?~") ? b.Split("~?~", StringSplitOptions.None)[0] : b, 
                    TargetNames = b.Contains("~?~") ? b.Split("~?~", StringSplitOptions.None)[1].Split('|').Select(n=>n.Replace("*","")).ToList() : new List<string>() { b },
                    TargetsRequiredForKill = b.Contains("~?~") ? (b.Split("~?~", StringSplitOptions.None)[1].Split('|').Count(n => n.Contains("*")) > 0 ? 
                        b.Split("~?~", StringSplitOptions.None)[1].Split('|').Where(n => n.Contains("*")).Select(n => n.Replace("*", "")).ToList()
                        : b.Split("~?~", StringSplitOptions.None)[1].Split('|').Select(n => n.Replace("*", "")).ToList()) : new List<string>() { b }

                }).ToList();
            } 
        }
        public bool IsBossEncounter => BossInfos?.Count != 0;
        public List<BossInfo> BossInfos { get; set; } 

    }
}
