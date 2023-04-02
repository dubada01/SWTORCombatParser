using System;
using System.Collections.Generic;
using System.Linq;

namespace SWTORCombatParser.DataStructures.EncounterInfo
{
    public enum EncounterType
    {
        Flashpoint,
        Operation,
        Lair,
        Parsing,
        Warzone,
        Huttball,
        Arena
    }
    public class BossInfo
    {
        private List<string> targetsRequiredForKill = new List<string>();

        public string EncounterName { get; set; }
        public List<string> TargetIds { get; set; } = new List<string>();
        public List<string> TargetsRequiredForKill { 
            get 
            {
                if (!targetsRequiredForKill.Any())
                    return TargetIds;
                return targetsRequiredForKill;
            } 
            set => targetsRequiredForKill = value; }
    }

    public class MapInfo
    {
        public double MinX;
        public double MaxX;
        public double MinY;
        public double MaxY;
    }
    public class EncounterInfo
    {
        private List<string> bossNames = new List<string>();
        private Dictionary<string, Dictionary<string, List<long>>> bossIds = new Dictionary<string, Dictionary<string, List<long>>>();
        private string _difficutly = "Story";
        private string _numberOfPlayer = "4";
        private Dictionary<string, Dictionary<string, List<long>>> requiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>();

        public static EncounterInfo GetCopy(EncounterInfo source)
        {
            return new EncounterInfo
            {
                LogName = source.LogName,
                LogId = source.LogId,
                Name = source.Name,
                BossNames = source.BossNames,
                BossIds = source.BossIds,
                EncounterType = source.EncounterType,
                BossInfos = source.BossInfos,
                MapInfo = source.MapInfo,
                RequiredIdsForKill= source.RequiredIdsForKill,

            };
        }

        public string Difficutly
        {
            get => _difficutly;
            set
            {
                _difficutly = value;

            }
        }

        public string NumberOfPlayer
        {
            get => _numberOfPlayer;
            set
            {
                _numberOfPlayer = value;
                BossInfos = GetBossInfos();
            }
        }


        public EncounterType EncounterType { get; set; }
        public string LogName { get; set; }
        public string LogId { get; set; }
        public string NamePlus => GetNamePlus();
        public string Name { get; set; }
        public MapInfo MapInfo { get; set; }
        public List<string> BossNames
        {
            get => bossNames;
            set => bossNames = value ?? new List<string>();
        }
        public Dictionary<string, Dictionary<string, List<long>>> RequiredIdsForKill { get => requiredIdsForKill; set => requiredIdsForKill = value ?? new Dictionary<string, Dictionary<string, List<long>>>(); }
        public Dictionary<string, Dictionary<string, List<long>>> BossIds
        {
            get => bossIds;
            set => bossIds = value ?? new Dictionary<string, Dictionary<string, List<long>>>();
        }
        private List<BossInfo> GetBossInfos()
        {
            if (!RequiredIdsForKill.Any())
            {
                RequiredIdsForKill = bossIds;
            }
            else
            {

            }
            if (bossIds.Count > 0)
            {
                return BossIds.Select(bi => new BossInfo()
                {
                    EncounterName = bi.Key,
                    TargetIds = bi.Value[GetKey(bi.Value.Keys.ToList())].Select(id => id.ToString()).ToList(),
                    TargetsRequiredForKill = RequiredIdsForKill[bi.Key][GetKey(bi.Value.Keys.ToList())].Select(id => id.ToString()).ToList()
                }).ToList();
            }
            if (bossNames.Count == 0)
                return new List<BossInfo>();
            return BossNames.Select(b => new BossInfo()
            {
                EncounterName = b.Contains("~?~") ? b.Split("~?~")[0] : b,
                TargetIds = b.Contains("~?~") ? b.Split("~?~")[1].Split('|').Select(n => n.Replace("*", "")).ToList() : new List<string>() { b },

            }).ToList();
        }

        private string GetKey(List<string> availableModes)
        {
            if (NumberOfPlayer.Contains("4") && availableModes.All(m => m == "All"))
                return "All";
            if (NumberOfPlayer.Contains("4") && availableModes.All(m => m != "All"))
                return ((Difficutly == "Story" ? "Veteran" : Difficutly) + " " + NumberOfPlayer.Split(" ")[0]);
            return ((Difficutly == "Master" ? "Veteran" : Difficutly) + " " + NumberOfPlayer.Split(" ")[0]);
        }

        public bool IsBossEncounter => BossInfos?.Count != 0;
        public bool IsPvpEncounter => (int)EncounterType >= 4;
        public List<BossInfo> BossInfos { get; set; } = new List<BossInfo>();

        private string GetNamePlus()
        {
            if ((int)EncounterType < 4)
            {
                return Name + $" {{{NumberOfPlayer} {Difficutly}}}";
            }
            else
            {
                return Name + $" {{{EncounterType.ToString()}}}";
            }
        }
    }
}
