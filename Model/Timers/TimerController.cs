using Newtonsoft.Json;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using SWTORCombatParser.ViewModels.Avalonia_TEMP;
using Timer = SWTORCombatParser.DataStructures.Timer;

namespace SWTORCombatParser.Model.Timers;

public static class TimerController
{
    private static bool historicalParseFinished;
    private static DateTime _startTime;
    private static string _currentDiscipline;
    private static List<TimerInstance> _availableTimers = new List<TimerInstance>();
    private static List<TimerInstance> _filteredTimers = new List<TimerInstance>();
    private static ConcurrentDictionary<string,TimerInstanceViewModel> _currentlyActiveTimers = new();
    private static bool _timersEnabled;
    private static EncounterInfo _currentEncounter;
    private static object _timerLock = new object();

    private static List<IDisposable> _showTimerSubs = new List<IDisposable>();
    private static List<IDisposable> _hideTimerSubs = new List<IDisposable>();
    private static List<IDisposable> _reorderSubs = new List<IDisposable>();
    public static event Action<TimerInstanceViewModel, Action<TimerInstanceViewModel>> TimerExpired = delegate { };
    public static event Action<TimerInstanceViewModel, Action<TimerInstanceViewModel>> TimerTriggered = delegate { };
    public static event Action<string> ReorderRequested = delegate { };
    public static event Action TimersInitialized = delegate { };
    public static void Init()
    {
        CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
        CombatLogStreamer.HistoricalLogsStarted += HistoricalStarted;
        CombatLogStreamer.CombatUpdated += CombatStateUpdated;
        CombatLogStreamer.NewLineStreamed += NewLogStreamed;
        CombatLogStateBuilder.AreaEntered += AreaChanged;
        DefaultOrbsTimersManager.Init();
        RefreshAvailableTimers(true);

    }

    private static void AreaChanged(EncounterInfo obj)
    {
        _currentEncounter = obj;
        FilterTimers();
    }
    private static void TrySetEncounter(EncounterInfo obj)
    {
        if (obj != _currentEncounter)
        {
            _currentEncounter = obj;
            FilterTimers();
        }
    }
    private static void TrySetBoss((string, string, string) bossinfo)
    {
        if (bossinfo != _currentBoss && !string.IsNullOrEmpty(bossinfo.Item1))
        {
            _currentBoss = bossinfo;
            
            //todo DELETE THIS. Using it to test the avalonia UI and trigger a new combat with a boss starting;
            AvaloniaTimelineBuilder.StartBoss(_currentBoss.Item1);
            
            FilterTimers();
        }
    }
    private static bool CheckEncounterAndBoss(TimerInstance t)
    {
        var timerEncounter = t.SourceTimer.SpecificEncounter;
        if (_currentEncounter.Name != timerEncounter)
            return false;
        var supportedDifficulties = new List<string>();
        if (t.SourceTimer.ActiveForStory)
            supportedDifficulties.Add("Story");
        if (t.SourceTimer.ActiveForVeteran)
            supportedDifficulties.Add("Veteran");
        if (t.SourceTimer.ActiveForMaster)
            supportedDifficulties.Add("Master");
        var timerBoss = t.SourceTimer.SpecificBoss;
        if (timerEncounter == "All")
            return true;
        if (string.IsNullOrEmpty(_currentBoss.Item1))
        {
            return false;
        }
        if ((supportedDifficulties.Contains(_currentEncounter.Difficutly)) && _currentBoss.Item1.ToLower() == timerBoss.ToLower())
            return true;
        return false;
    }
    private static void FilterTimers()
    {
        if (Monitor.TryEnter(_timerLock, 100))
        {
            try
            {
                _filteredTimers.Clear();
                foreach (var timer in _availableTimers)
                {
                    if (timer.SourceTimer.TrackOutsideOfCombat)
                        _filteredTimers.Add(timer);
                    if (timer.SourceTimer.TimerSource.Contains("|") && _currentEncounter != null)
                    {
                        if (CheckEncounterAndBoss(timer) && !_filteredTimers.Any(t => t.SourceTimer.Id == timer.SourceTimer.Id))
                        {
                            _filteredTimers.Add(timer);
                        }
                    }
                    else
                    {
                        if (!_filteredTimers.Any(t => t.SourceTimer.Id == timer.SourceTimer.Id))
                            _filteredTimers.Add(timer);
                    }
                }
            }
            finally { Monitor.Exit(_timerLock); }
        }
    }

    private static void HistoricalStarted()
    {
        historicalParseFinished = false;
    }

    private static List<string> _expirationTimers = new List<string>();
    public static void RefreshAvailableTimers(bool initializing = false)
    {
        Task.Run(() =>
        {
            _expirationTimers.Clear();
            _hideTimerSubs.ForEach(s => s.Dispose());
            _showTimerSubs.ForEach(s => s.Dispose());
            _reorderSubs.ForEach(s => s.Dispose());
            var allDefaults = DefaultOrbsTimersManager.GetAllDefaults();
            var timers = allDefaults.SelectMany(t => t.Timers);

            List<Timer> secondaryTimers = new List<Timer>();
            GetAllSubTimers(ref secondaryTimers, timers.ToList());
            var distinctTimers = secondaryTimers.DistinctBy(t => t.Id);


            _availableTimers = distinctTimers.Where(t => t.IsEnabled).Select(t => new TimerInstance(t.Copy())).ToList();
            foreach (var timerInstance in _availableTimers)
            {
                if (timerInstance.SourceTimer.IsSubTimer)
                {
                    var parentTimer =
                        _availableTimers.FirstOrDefault(t => t.SourceTimer.Id == timerInstance.ParentTimerId);
                    if (parentTimer != null)
                    {
                        timerInstance.ParentTimer = parentTimer;
                    }
                    else
                    {
                        Logging.LogInfo("Parent timer not found for: " + JsonConvert.SerializeObject(timerInstance));
                    }
                }

                if (!string.IsNullOrEmpty(timerInstance.ExperiationTimerId))
                {
                    var trigger =
                        _availableTimers.FirstOrDefault(t => t.SourceTimer.Id == timerInstance.ExperiationTimerId);
                    if (trigger != null)
                    {
                        //timerInstance.ExpirationTimer = trigger;
                        _expirationTimers.Add(trigger.SourceTimer.Id);
                    }
                    else
                    {
                        Logging.LogInfo("Expiration timer not found for: " +
                                        JsonConvert.SerializeObject(timerInstance));
                    }
                }

                if (!string.IsNullOrEmpty(timerInstance.CancellationTimerId))
                {
                    var cancelTrigger =
                        _availableTimers.FirstOrDefault(t => t.SourceTimer.Id == timerInstance.CancellationTimerId);
                    if (cancelTrigger != null)
                    {
                        timerInstance.CancelTimer = cancelTrigger;
                    }
                    else
                    {
                        Logging.LogInfo("Cancel timer not found for: " + JsonConvert.SerializeObject(timerInstance));
                    }
                }
            }

            _hideTimerSubs = _availableTimers.Select(t =>
                Observable
                    .FromEvent<Action<TimerInstanceViewModel,bool>, (TimerInstanceViewModel timer, bool ended)>(
                        h => (p1,p2) => h((p1,p2)),
                        h => t.TimerOfTypeExpired += h,
                        h => t.TimerOfTypeExpired -= h
                    )
                    // ← everything after ObserveOn runs on the ThreadPool
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Subscribe(args => OnTimerExpired(args.timer, args.ended))
            ).ToList();
            _showTimerSubs = _availableTimers.Select(t =>
                Observable.FromEvent<TimerInstanceViewModel>(handler => t.NewTimerInstance += handler,
                    handler => t.NewTimerInstance -= handler)
                    .ObserveOn(TaskPoolScheduler.Default).Subscribe(AddTimerVisual)).ToList();
            _reorderSubs = _availableTimers.Select(t =>
                Observable.FromEvent<string>(handler => t.ReorderRequested += handler,
                        handler => t.ReorderRequested -= handler).Throttle(TimeSpan.FromMilliseconds(250))
                    .ObserveOn(TaskPoolScheduler.Default)
                    .Subscribe(ReorderRequest)).ToList();
            FilterTimers();
            if(initializing)
                TimersInitialized();
        });

    }

    private static void GetAllSubTimers(ref List<Timer> subTimers, List<Timer> baseTimers)
    {
        foreach (var timer in baseTimers)
        {
            foreach (var obj in FlattenNestedObjects(timer))
            {
                subTimers.Add(obj);
            }
        }
    }

    private static List<Timer> FlattenNestedObjects(Timer timer)
    {
        var result = new List<Timer> { timer };
        if (timer.Clause1 != null)
        {
            foreach (var nestedTimer in FlattenNestedObjects(timer.Clause1))
            {
                result.Add(nestedTimer);
            }
        }
        if (timer.Clause2 != null)
        {
            foreach (var nestedTimer in FlattenNestedObjects(timer.Clause2))
            {
                result.Add(nestedTimer);
            }
        }

        return result;
    }
    public static ConcurrentDictionary<string,TimerInstanceViewModel> GetActiveTimers()
    {
        return _currentlyActiveTimers;
    }

    private static void ReorderRequest(string id)
    {
        ReorderRequested.InvokeSafely(id);
    }
    private static void AddTimerVisual(TimerInstanceViewModel t)
    {
        if (t.SourceTimer.IsSubTimer)
            TimerAddedCallback(t);
        else
            TimerTriggered.InvokeSafely(t, TimerAddedCallback);
    }

    private static object _currentTimersModLock = new object();
    private static (string, string, string) _currentBoss;

    private static void TimerAddedCallback(TimerInstanceViewModel addedTimer)
    {
        _currentlyActiveTimers.TryAdd(addedTimer.SourceTimer.Id, addedTimer);
    }
    private static void OnTimerExpired(TimerInstanceViewModel t, bool endedNaturally)
    {
        TimerInstance[] toNotify;

        lock (_timerLock)
        {
            var id = t.SourceTimer.Id;
            var timersThatCare = _filteredTimers.Where(x => x.ExperiationTimerId == id);
            toNotify = timersThatCare.ToArray();
        }

        // Now we're _outside_ the lock
        foreach (var timer in toNotify)
            timer.ExpirationTimerEnded(t, endedNaturally);

        // this must also be outside the lock
        TimerExpired.InvokeSafely(t, TimerRemovedCallback);
    }

    private static void TimerRemovedCallback(TimerInstanceViewModel removedTimer)
    {
        _currentlyActiveTimers.Remove(removedTimer.SourceTimer.Id, out _);
    }

    private static void EnableTimers(DateTime combatEndTime, bool localPlayerIdentified)
    {
        historicalParseFinished = true;
        _timersEnabled = true;
    }
    private static void NewLogStreamed(ParsedLogEntry log)
    {
        lock (_timerLock)
        {
            TrySetEncounter(CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp));
            TrySetBoss(CombatIdentifier.GetCurrentBossInfo(new HashSet<ParsedLogEntry>() { log }, _currentEncounter));
            _currentDiscipline ??= CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(log.TimeStamp).Discipline;
            var currentTarget = CombatLogStateBuilder.CurrentState.GetLocalPlayerTargetAtTime(log.TimeStamp).Entity;
            foreach (var timer in _filteredTimers)
            {
                if (!timer.TrackOutsideOfCombat && !CombatLogStreamer.InCombat)
                    continue;
                timer.CheckForTrigger(log, _startTime, _currentDiscipline, _currentlyActiveTimers, _currentEncounter, _currentBoss, currentTarget);
            }
        }
    }

    private static void CombatStateUpdated(CombatStatusUpdate obj)
    {
        if (!historicalParseFinished)
            return;
        if (obj.Type == UpdateType.Start)
        {
            _currentBoss = ("", "", "");
            OrbsVariableManager.ResetVariables();
            _startTime = obj.CombatStartTime;
            UncancellBeforeCombat();
            _currentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(obj.CombatStartTime).Discipline;
        }
        if (obj.Type == UpdateType.Stop)
        {
            CancelAfterCombat();
        }
    }
    private static void UncancellBeforeCombat()
    {
        foreach (var timer in _filteredTimers)
        {
            timer.UnCancel();
        }
    }
    private static void CancelAfterCombat()
    {
        foreach (var timer in _filteredTimers.Where(t => !t.SourceTimer.TrackOutsideOfCombat))
        {
            timer.Cancel();
            timer.CombatEnd();
        }
    }

    internal static void TryTriggerTimer(CombatModifier combatModifier)
    {
        var timer = _filteredTimers.First(t => t.SourceTimer.Effect == combatModifier.EffectId.ToString());
        timer.CreateTimerInstance(combatModifier.StartTime, combatModifier.Target.Name, combatModifier.Target.Id, combatModifier.ChargesAtTime.MaxBy(t => t.Key).Value);
    }
}