using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.DataStructures.Timeline;
using SWTORCombatParser.Model.CloudRaiding;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.ViewModels.Combat_Monitoring;

namespace SWTORCombatParser.ViewModels.Avalonia_TEMP;

public static class AvaloniaTimelineBuilder
{
    private static bool _unlocked;
    private static TimelineWindowViewModel _timelineWindowViewModel;
    private static bool _inBossInstance = false;
    private static string _currentBossName = "";
    private static DateTime _lastEncounterStartTime;
    private static EncounterInfo _currentEncounter;
    private static bool _timelineEnabled;
    private static bool _timeTrackingLive;

    public static void Init()
    {
        _timelineWindowViewModel = new TimelineWindowViewModel("Timeline");

        CombatLogStateBuilder.AreaEntered += TryBuildTimeline;
        CombatIdentifier.CombatFinished += CombatFinished;
        CombatLogStreamer.HistoricalLogsFinished += HistoricalLogsParsed;
        CombatSelectionMonitor.CombatSelected += ShowTimelineNonLive;

        var defaults = DefaultGlobalOverlays.GetOverlayInfoForType("Timeline");
        TimelineEnabled = defaults.Acive;
    }

    private static void HistoricalLogsParsed(DateTime arg1, bool arg2)
    {
        var lastEncounter = CombatLogStateBuilder.CurrentState.EncounterEnteredInfo.LastOrDefault();
        if (lastEncounter.Value != null && lastEncounter.Value.IsBossEncounter &&
            CombatMonitorViewModel.IsLiveParseActive() && DateTime.Now - lastEncounter.Key < TimeSpan.FromMinutes(90))
        {
            TryBuildTimeline(lastEncounter.Value);
        }
        else
        {
            HideTimelineOverlay();
        }
    }


    private static void CombatFinished(Combat obj)
    {
        if(_currentEncounter == null)
            return;
        if (!_inBossInstance || !obj.IsCombatWithBoss || !_currentEncounter.BossInfos.Any(bi=>bi.EncounterName == obj.EncounterBossDifficultyParts.Item1) || _lastEncounterStartTime > obj.StartTime || !_timeTrackingLive)
            return;

        if (!obj.WasBossKilled)
            RemoveBoss(obj.EncounterBossDifficultyParts.Item1);
        if (obj.WasBossKilled)
        {
            _timelineWindowViewModel.BossKilled(obj.EncounterBossDifficultyParts.Item1,(obj.StartTime - _lastEncounterStartTime),(obj.EndTime - _lastEncounterStartTime));
        }
    }

    public static bool TimelineEnabled
    {
        get => _timelineEnabled;
        set
        {
            _timelineEnabled = value;
            if (value)
            {
                if(CombatMonitorViewModel.IsLiveParseActive())
                    HistoricalLogsParsed(DateTime.Now, false);
                else
                {
                    if(CombatIdentifier.CurrentCombat!=null && CombatIdentifier.CurrentCombat.StartTime != DateTime.MinValue)
                        ShowTimelineNonLive(CombatIdentifier.CurrentCombat);
                }

                if (_unlocked)
                {
                    _timelineWindowViewModel.InBossInstance = true;
                    _timelineWindowViewModel.SetClickThrough(false);
                }
            }
            else
            {
                _timelineWindowViewModel.InBossInstance = false;
            }
            _timelineWindowViewModel.Active = value;
        }
    }

    public static async Task UploadBossKill(string encounterName, string flashpointOrRaidName, string difficulty, string playerCount, DateTime startTime, DateTime endTime)
    {
        if(!_timeTrackingLive)
            return;
        var timeTrialInfo = new TimeTrialLeaderboardEntry()
        {
            BossFight = encounterName,
            Timestamp = endTime,
            StartSeconds = (int)startTime.Subtract(_lastEncounterStartTime).TotalSeconds,
            EndSeconds = (int)endTime.Subtract(_lastEncounterStartTime).TotalSeconds,
            PlayerName = CombatLogStateBuilder.CurrentState.LocalPlayer.Name,
            Encounter = flashpointOrRaidName,
            Difficulty = difficulty,
            PlayerCount = playerCount
        };
        await API_Connection.AddNewTimeTrialEntry(timeTrialInfo);
        BuildTimelineFromEncounter();
    }

    public static void RemoveBoss(string bossName)
    {
        if(_inBossInstance)
            _timelineWindowViewModel.RemoveBoss(bossName);
    }
    public static void StartBoss(string bossName)
    {
        if (_inBossInstance)
        {
            if (_currentEncounter == null)
                return;
            if((_currentEncounter.BossInfos.First().EncounterName == bossName || _currentEncounter.NumberOfPlayer == "4") && !_timeTrackingLive)
                _timeTrackingLive = true;
            if(_currentEncounter.NumberOfPlayer != "4" && _currentEncounter.Difficutly != "Story")
                _timeTrackingLive = false;
            
            if(!_timeTrackingLive)
                return;
            _timelineWindowViewModel.StartNewBoss(bossName,DateTime.Now - _lastEncounterStartTime);
            _currentBossName = bossName;
        }
    }
    private static void ShowTimelineNonLive(Combat selectedCombat)
    {
        if(CombatMonitorViewModel.IsLiveParseActive())
            return;
        var encounter = selectedCombat.ParentEncounter;
        if (encounter.IsBossEncounter)
        {
            if (_currentEncounter != encounter)
            {
                _timelineWindowViewModel.Reset();
            }

            _currentEncounter = encounter;
            BuildTimelineFromEncounter(false);
            
            _timelineWindowViewModel.BossKilled(selectedCombat.EncounterBossDifficultyParts.Item1,
                (selectedCombat.StartTime - _lastEncounterStartTime),
                (selectedCombat.EndTime - _lastEncounterStartTime));
        }
        else
        {
            HideTimelineOverlay();
        }
    }
    private static void TryBuildTimeline(EncounterInfo obj)
    {
        if(obj.IsBossEncounter && (obj.Difficutly == "Story" || obj.NumberOfPlayer == "4"))
        {
            if (_currentEncounter != obj)
            {
                _timeTrackingLive = false;
                _timelineWindowViewModel.Reset();
            }
            _currentEncounter = obj;
            BuildTimelineFromEncounter();
        }
        else
        {
            _timeTrackingLive = false;
            //HideTimelineOverlay();
        }
        _inBossInstance = obj.IsBossEncounter;
        _timelineWindowViewModel.InBossInstance = obj.IsBossEncounter;
    }

    private static void BuildTimelineFromEncounter(bool showLive = true)
    {
        _inBossInstance = true;
        _lastEncounterStartTime = CombatLogStateBuilder.CurrentState.EncounterEnteredInfo.FirstOrDefault(kvp=>kvp.Value == _currentEncounter).Key;
        var timeTrialLeaderboardEntries = _currentEncounter.BossInfos.Select(async bi =>
        {
            var timeTrialInfoForBoss = await API_Connection.GetTimeTrialEntriesForBoss(bi.EncounterName, _currentEncounter.Name, _currentEncounter.Difficutly, _currentEncounter.NumberOfPlayer);
            // Calculate the 10th percentile index
            int tenthPercentileObject = (int)Math.Ceiling(timeTrialInfoForBoss.Count * 0.1) - 1;
            tenthPercentileObject = Math.Max(0, tenthPercentileObject); // Ensure the index is not negative

            return new BossKillInfo()
            {
                BossName = bi.EncounterName,
                StartTime = timeTrialInfoForBoss.Count == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(timeTrialInfoForBoss.OrderBy(t => t.StartSeconds).ElementAt(tenthPercentileObject).StartSeconds),
                EndTime = timeTrialInfoForBoss.Count == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(timeTrialInfoForBoss.OrderBy(t => t.EndSeconds).ElementAt(tenthPercentileObject).EndSeconds),
                IsKilled = true,
            };
        });
        //need to get the value of the result from each of the tasks in the resulting list
        Task.WhenAll(timeTrialLeaderboardEntries).ContinueWith((t) =>
        {
            var bossKillInfos = t.Result;
            var maxDuration = bossKillInfos.Max(b => b.EndTime);
            DisplayTimelineOverlay(maxDuration, bossKillInfos.ToList(),showLive);
        });
    }
    // In your WPF code, when you want to initialize Avalonia and open the window
    public static void DisplayTimelineOverlay(TimeSpan maxDuration, List<BossKillInfo> previousKills,bool showLive)
    {
        Dispatcher.UIThread.Invoke(() =>
        {        
            _timelineWindowViewModel.ConfigureTimeline(maxDuration,previousKills,_currentEncounter.Name, _currentEncounter.Difficutly, _currentEncounter.NumberOfPlayer);
            _timelineWindowViewModel.ShowOverlayWindow();
            if (showLive)
                StartEncounterTask();
        });
    }

    public static void HideTimelineOverlay()
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            _timelineWindowViewModel.HideOverlayWindow();
        });
    }

    public static void UnlockOverlay()
    {        
        _unlocked = true;
        if(_timelineEnabled)
            _timelineWindowViewModel.ShowOverlayWindow();
        _timelineWindowViewModel.SetClickThrough(false);

    }
    public static void LockOverlay()
    {
        _unlocked = false;
        _timelineWindowViewModel.SetClickThrough(true);
        if(!_inBossInstance)
            _timelineWindowViewModel.HideOverlayWindow();
    }
    
    private static void StartEncounterTask()
    {
        //start a task that runs and updates the timeline every second it will need to be able to be cancelled when the player leaves the instance
        Task.Run(() =>
        {
            while (_inBossInstance)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    _timelineWindowViewModel.UpdateTimeline(DateTime.Now - _lastEncounterStartTime);
                });
                Task.Delay(1000).Wait();
            }
        });

    }
}