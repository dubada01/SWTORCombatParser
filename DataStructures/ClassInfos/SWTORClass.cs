using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SWTORCombatParser.DataStructures
{
    public class Ability
    {
        public string Name { get; set; }
        public bool Threatless { get; set; }
        public bool StaticThreat { get; set; }

    }
    public enum Role
    {
        Tank,
        Healer,
        DPS
    }
    public class SWTORClass
    {
        public string Name { get; set; }
        public string Discipline { get; set; }
        public Role Role { get; set; }
        public List<string> UniqueAbilities { get; set; }
        public List<Ability> Abilities { get; set; }
    }

}
