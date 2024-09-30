using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SWTORCombatParser.DataStructures.Timeline;

namespace SWTORCombatParser.ViewModels.Avalonia_TEMP;

public class TimelineElement
    {
        public string BossName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan TTK { get; set; }
        public bool IsLeaderboard { get; set; }
        public bool IsFreshKill { get; set; }
    }

    public class TimelineWindowViewModel : ViewModelBase
    {
        private object lockObj = new object();
        public event Action<TimeSpan> OnUpdateTimeline = delegate { };
        public event Action<bool> UpdateClickThrough = delegate { }; 
        public event Action<TimeSpan> OnInit = delegate { };
        public event Action<string,string,string> AreaEntered = delegate { };
        private InstanceInformation _instanceInfo;
        public ObservableCollection<TimelineElement> AllTimelineElements { get; } = new ObservableCollection<TimelineElement>();

        // Expose CurrentTime and MaxDuration as properties
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan MaxDuration => _instanceInfo?.MaxDuration ?? TimeSpan.Zero;

        public void ConfigureTimeline(TimeSpan maxDuration, List<BossKillInfo> previousKills, string areaName, string difficulty, string playerCount)
        {
            lock (lockObj)
            {
                _instanceInfo.MaxDuration = maxDuration;
                _instanceInfo.PreviousBossKills = previousKills;
                UpdateBossKillElements();
                OnInit(maxDuration);
                OnUpdateTimeline(maxDuration);
                AreaEntered(areaName, difficulty, playerCount);
            }
        }

        public void UpdateBossKillElements()
        {
            AllTimelineElements.Clear();
            //add each boss kill element for previous bosses
            foreach (var boss in _instanceInfo.PreviousBossKills)
            {
                AllTimelineElements.Add(new TimelineElement
                {
                    BossName = boss.BossName,
                    StartTime = boss.StartTime,
                    TTK = boss.TTK,
                    IsLeaderboard = true
                });
            }

            // add each boss kill element for current bosses
            foreach (var boss in _instanceInfo.CurrentBossKills)
            {
                AllTimelineElements.Add(new TimelineElement
                {
                    BossName = boss.BossName,
                    StartTime = boss.StartTime,
                    TTK = boss.TTK,
                    IsFreshKill = boss.IsKilled
                });
            }
        }

        public void SetClickThrough(bool canClickThrough)
        {
            UpdateClickThrough(canClickThrough);
        }
        // Call this method whenever the data updates in real-time
        public void UpdateTimeline(TimeSpan  currentTime)
        {
            lock (lockObj)
            {
                CurrentTime = currentTime;
                if(MaxDuration < currentTime)
                {
                    _instanceInfo.MaxDuration = currentTime;
                }
                OnUpdateTimeline(currentTime);
                //also update any active boss encounters to have their end time be the current time
                foreach (var boss in _instanceInfo.CurrentBossKills.Where(b=>b.IsKilled == false))
                {
                    boss.EndTime = currentTime;
                }

                UpdateBossKillElements();
            }
        }

        public void Reset()
        {
            lock (lockObj)
            {
                _instanceInfo = new InstanceInformation()
                {
                    MaxDuration = TimeSpan.Zero,
                    PreviousBossKills = new List<BossKillInfo>(),
                    CurrentBossKills = new List<BossKillInfo>()
                };
                UpdateBossKillElements();
            }
        }
        public void RemoveBoss(string bossName)
        {
            lock (lockObj)
            {
                _instanceInfo.CurrentBossKills.RemoveAll(b => b.BossName == bossName && !b.IsKilled);
                UpdateBossKillElements();
            }
        }
        public void StartNewBoss(string bossName, TimeSpan startTime)
        {
            lock (lockObj)
            {
                _instanceInfo.CurrentBossKills.Add(new BossKillInfo
                {
                    BossName = bossName,
                    StartTime = startTime,
                    EndTime = startTime
                });
                UpdateBossKillElements(); 
            }
        }

        public void BossKilled(string bossName, TimeSpan startTime, TimeSpan killTime)
        {
            lock (lockObj)
            {
                RemoveBoss(bossName);
                _instanceInfo.CurrentBossKills.Add(new BossKillInfo()
                {
                    BossName = bossName,
                    StartTime = startTime,
                    EndTime = killTime,
                    IsKilled = true
                });
                if(killTime > _instanceInfo.MaxDuration)
                {
                    _instanceInfo.MaxDuration = killTime;
                }
                UpdateBossKillElements();
            }
        }
        public void AddBossWipe(string bossName, TimeSpan startTime, TimeSpan killTime)
        {
            lock (lockObj)
            {
                RemoveBoss(bossName);
                _instanceInfo.CurrentBossKills.Add(new BossKillInfo()
                {
                    BossName = bossName,
                    StartTime = startTime,
                    EndTime = killTime,
                    IsKilled = false
                });
                if(killTime > _instanceInfo.MaxDuration)
                {
                    _instanceInfo.MaxDuration = killTime;
                }
                UpdateBossKillElements();
            }
        }
    }