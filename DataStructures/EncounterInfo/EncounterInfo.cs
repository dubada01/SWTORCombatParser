using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SWTORCombatParser.DataStructures.RaidInfos
{
    public class BossInfo
    {
        public string EncounterName { get; set; }
        public List<string> TargetNames { get; set; }
    }
    public class EncounterInfo
    {
        public string Difficutly;
        public string NumberOfPlayer;
        public string LogName { get; set; }
        public string Name { get; set; }
        public List<string> BossNames { get; set; }
        public List<BossInfo> BossInfos => BossNames.Select(b => new BossInfo() { EncounterName = b.Contains("~?~") ? b.Split("~?~", StringSplitOptions.None)[0] : b, TargetNames = b.Contains("~?~") ? b.Split("~?~", StringSplitOptions.None)[1].Split('|').ToList() : new List<string>() { b } }).ToList();

    }
}
