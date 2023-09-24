using System.Collections.Generic;

namespace SWTORCombatParser.DataStructures.ClassInfos
{
    public class Ability
    {
        public string Name { get; set; }
        public bool Threatless { get; set; }
        public bool StaticThreat { get; set; }

    }
    public enum Role
    {
        Unknown,
        DPS,
        Tank,
        Healer
    }
    public class SWTORClass
    {
        public string Name { get; set; }
        public string Discipline { get; set; }
        public string DisciplineId { get; set; }
        public bool IsRanged { get; set; }
        public Role Role { get; set; }
        public List<string> UniqueAbilities { get; set; }
        public List<Ability> SpecialThreatAbilities { get; set; }
    }

}
