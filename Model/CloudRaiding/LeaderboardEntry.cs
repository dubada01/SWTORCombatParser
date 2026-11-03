using System;
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
    public class LeaderboardValueStats
    {
        public double Median { get; set; }
        public double StandardDev { get; set; }
        public int EntryCount { get; set; }
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
        public List<string> PlayerNames { get; set; }
        public int TimeToKill { get; set; }
        public string Software_Version { get; set; }
    }

    public class TimeTrialLeaderboardEntry
    {
        public int Id { get; set; }
        public string BossFight { get; set; }
        public string PlayerName { get; set; }
        public int StartSeconds { get; set; }
        public int EndSeconds { get; set; }
        public DateTime Timestamp { get; set; }
        public string Encounter { get; set; }
        public string Difficulty { get; set; }
        public string PlayerCount { get; set; }
    }
}
