using System;
using System.Collections.Generic;

namespace SWTORCombatParser.DataStructures.Timeline;

public class BossKillInfo
{
    public TimeSpan TTK => EndTime - StartTime;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string BossName { get; set; } = "Test Boss";
    public bool IsKilled { get; set; } = false;
}
public class InstanceInformation
{
    public TimeSpan MaxDuration { get; set; }
    public List<BossKillInfo> PreviousBossKills { get; set; } = new List<BossKillInfo>();
    public List<BossKillInfo> CurrentBossKills { get; set; } = new List<BossKillInfo>();
}