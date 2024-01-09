﻿using System;
using System.Collections.Generic;

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
    public class LeaderboardVersion
    {
        public DateTime timestamp { get; set; }
        public int leadeboard_version { get; set; }
    }
    public class LeaderboardEntry
    {
        public DateTime TimeStamp { get; set; }
        public int Duration { get; set; }
        public string Boss { get; set; }
        public string Encounter { get; set; }
        public string Character { get; set; }
        public string Class { get; set; }
        public double Value { get; set; }
        public LeaderboardEntryType Type { get; set; }
        public string Version { get; set; }
        public bool VerifiedKill { get; set; }
        public string Logs { get; set; }
    }
    public class GameEncounter
    {
        public DateTime EncounterTimestamp { get; set; }
        public string BossName { get; set; }
        public string Difficulty { get; set; }
        public string EncounterName { get; set; }
        public int NumberOfPlayers { get; set; }
        public List<string> PlayerClasses { get; set; }
        public int TimeToKill { get; set; }
    }
}
