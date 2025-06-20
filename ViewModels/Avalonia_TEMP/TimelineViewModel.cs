using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.Timeline;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Combat_Monitoring;
using SWTORCombatParser.ViewModels.DataGrid;
using SWTORCombatParser.Views.DataGrid_Views;
using SWTORCombatParser.Views.Overlay.Timeline;

namespace SWTORCombatParser.ViewModels.Avalonia_TEMP;

public class TimelineElement
    {
        public string BossName { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan TTK { get; set; }
        public bool IsLeaderboard { get; set; }
        public bool IsFreshKill { get; set; }
    }

    public class TimelineWindowViewModel : BaseOverlayViewModel
    {
        private object lockObj = new object();
        public event Action<TimeSpan> OnUpdateTimeline = delegate { };
        public event Action<TimeSpan> OnInit = delegate { };
        public event Action<string,string,string> AreaEntered = delegate { };
        private InstanceInformation _instanceInfo;
        private bool _inBossInstance;
        private readonly DataGridViewModel _metricViewModel;
        public ObservableCollection<TimelineElement> AllTimelineElements { get; } = new ObservableCollection<TimelineElement>();
        public DataGridView MetricsView { get; set; }

        // Expose CurrentTime and MaxDuration as properties
        public TimeSpan CurrentTime { get; set; }
        public TimeSpan MaxDuration => _instanceInfo?.MaxDuration ?? TimeSpan.Zero;
        public override bool ShouldBeVisible => InBossInstance;
        
        public bool InBossInstance
        {
            get => _inBossInstance;
            set
            {
                _inBossInstance = value;
                UpdateVisibility();
            }
        }

        public TimelineWindowViewModel(string overlayName) : base(overlayName)
        {
            MainContent = new TimelineWindow(this);
            _metricViewModel = new DataGridViewModel();
            MetricsView = new DataGridView(_metricViewModel);
            this.RaisePropertyChanged(nameof(MetricsView));
            _instanceInfo = new InstanceInformation()
            {
                MaxDuration = TimeSpan.Zero,
                PreviousBossKills = new List<BossKillInfo>(),
                CurrentBossKills = new List<BossKillInfo>()
            };
            EncounterMonitor.EncounterUpdated += UpdateEncounterLevelInfo;
        }

        private void UpdateEncounterLevelInfo(EncounterCombat obj)
        {
            _ = Task.Run(async () =>
            {
                var isActive = await Dispatcher.UIThread.InvokeAsync(() => MainContent.IsVisible);
                if (!isActive || !AvaloniaTimelineBuilder.TimelineEnabled)
                    return;

                if (obj == null || obj.Combats.Count == 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => _metricViewModel?.Reset());
                    return;
                }

                var overallCombat = await obj.GetOverallCombat();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _metricViewModel?.UpdateCombat(overallCombat);
                });
            });
        }


        public void ConfigureTimeline(TimeSpan maxDuration, List<BossKillInfo> previousKills, string areaName, string difficulty, string playerCount)
        {
            lock (lockObj)
            {
                _instanceInfo.MaxDuration = maxDuration;
                _instanceInfo.PreviousBossKills = previousKills;
                UpdateBossKillElements();
                OnInit.InvokeSafely(maxDuration);
                OnUpdateTimeline.InvokeSafely(maxDuration);
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
            OverlaysMoveable = !canClickThrough;
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
                OnUpdateTimeline.InvokeSafely(currentTime);
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