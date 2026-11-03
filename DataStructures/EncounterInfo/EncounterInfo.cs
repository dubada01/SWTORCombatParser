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
        Arena,
        OpenWorld
    }
    public class BossInfo
    {
        private List<long> targetsRequiredForKill = new List<long>();
        private string encounterName;
        public bool IsOpenWorld { get; set; }
        public string EncounterName
        {
            get => encounterName; set
            {
                IsOpenWorld = value.Contains("Open World");
                encounterName = value;
            }
        }
        public List<long> TargetIds { get; set; } = new List<long>();
        public ulong AbilityRequiredForKill { get; set; }
        public List<long> TargetsRequiredForKill
        {
            get
            {
                if (!targetsRequiredForKill.Any())
                    return TargetIds;
                return targetsRequiredForKill;
            }
            set => targetsRequiredForKill = value;
        }
    }

    public class MapInfo
    {
        public double MinX;
        public double MaxX;
        public double MinY;
        public double MaxY;
    }
    public class OpenWorldBoss
    {
        public string BossName { get; set; }
        public long BossId { get; set; }
    }
    public class EncounterInfo
    {
        private List<string> bossNames = new List<string>();
        private Dictionary<string, Dictionary<string, List<long>>> bossIds = new Dictionary<string, Dictionary<string, List<long>>>();
        private string _difficutly = "Story";
        private string _numberOfPlayer = "4";
        private Dictionary<string, Dictionary<string, List<long>>> requiredIdsForKill = new Dictionary<string, Dictionary<string, List<long>>>();
        private string name;
        private Dictionary<string, Dictionary<string, ulong>> requiredAbilitiesForKill;

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
                RequiredIdsForKill = source.RequiredIdsForKill,
                RequiredAbilitiesForKill = source.RequiredAbilitiesForKill,
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
        public bool IsOpenWorld { get; set; }
        public ulong LogId { get; set; }
        public string NamePlus => GetNamePlus();
        public string Name
        {
            get => name; set
            {
                IsOpenWorld = value.Contains("Open World");
                name = value;
            }
        }
        public MapInfo MapInfo { get; set; }
        public List<string> BossNames
        {
            get => bossNames;
            set => bossNames = value ?? new List<string>();
        }
        public Dictionary<string, Dictionary<string, ulong>> RequiredAbilitiesForKill { get => requiredAbilitiesForKill; set => requiredAbilitiesForKill = value ?? new Dictionary<string, Dictionary<string, ulong>>(); }
        public Dictionary<string, Dictionary<string, List<long>>> RequiredIdsForKill { get => requiredIdsForKill; set => requiredIdsForKill = value ?? new Dictionary<string, Dictionary<string, List<long>>>(); }
        public Dictionary<string, Dictionary<string, List<long>>> BossIds
        {
            get => bossIds;
            set => bossIds = value ?? new Dictionary<string, Dictionary<string, List<long>>>();
        }
        private List<BossInfo> GetBossInfos()
        {
            foreach (var boss in bossIds)
            {
                if (!RequiredIdsForKill.ContainsKey(boss.Key))
                {
                    RequiredIdsForKill.Add(boss.Key, boss.Value);
                }
            }
            if (bossIds.Count > 0)
            {
                return BossIds.Select(bi => new BossInfo()
                {
                    EncounterName = bi.Key,
                    TargetIds = bi.Value[GetKey(bi.Value, bi.Value.Keys.ToList())].Select(id => id).ToList(),
                    TargetsRequiredForKill = RequiredIdsForKill[bi.Key][GetKey(bi.Value, bi.Value.Keys.ToList())].Select(id => id).ToList(),
                    AbilityRequiredForKill = GetAbilityForKill(bi),
                }).ToList();
            }
            if (bossNames.Count == 0)
                return new List<BossInfo>();
            return BossNames.Select(b => new BossInfo()
            {
                EncounterName = b.Contains("~?~") ? b.Split("~?~")[0] : b,
                TargetIds = new List<long>()

            }).ToList();
        }

        private ulong GetAbilityForKill(KeyValuePair<string, Dictionary<string, List<long>>> bi)
        {
            if (!RequiredAbilitiesForKill.ContainsKey(bi.Key))
                return 0;
            if (!RequiredAbilitiesForKill[bi.Key].ContainsKey(GetKey(bi.Value, bi.Value.Keys.ToList())))
                return 0;
            return RequiredAbilitiesForKill[bi.Key][GetKey(bi.Value, bi.Value.Keys.ToList())];
        }

        private string GetKey(Dictionary<string, List<long>> self, List<string> availableModes)
        {
            if (NumberOfPlayer.Contains("4") && availableModes.All(m => m == "All"))
                return "All";
            if (NumberOfPlayer.Contains("4") && availableModes.All(m => m != "All"))
                return ((Difficutly == "Story" ? "Veteran" : Difficutly) + " " + NumberOfPlayer.Split(" ")[0]);
            if (self.Keys.Any(k => k.Contains(Difficutly)))
                return Difficutly + " " + NumberOfPlayer.Split(" ")[0];
            return ((Difficutly == "Master" ? "Veteran" : Difficutly) + " " + NumberOfPlayer.Split(" ")[0]);
        }
        public bool IsBossEncounter => BossInfos?.Count != 0 && !IsOpenWorld;
        public bool IsPvpEncounter => (int)EncounterType >= 4 && !IsOpenWorld;
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
