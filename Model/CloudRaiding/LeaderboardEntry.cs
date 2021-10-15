using System;
using System.Collections.Generic;
using System.Text;

namespace SWTORCombatParser.Model.CloudRaiding
{
    public enum LeaderboardEntryType
    {
        Damage,
        FocusDPS,
        Healing,
        EffectiveHealing,
        Mitigation
    }
    public class LeaderboardEntry
    {
        public int Duration { get; set; }
        public string Boss { get; set; }
        public string Character { get; set; }
        public string Class { get; set; }
        public double Value { get; set; }
        public LeaderboardEntryType Type { get; set; }
        public bool VerifiedKill { get; set; }
    }
}
