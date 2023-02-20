using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser.Model.Timers;

public static class TimerController
{
    private static bool historicalParseFinished;
    private static DateTime _startTime;
    private static string _currentDiscipline;
    private static List<TimerInstance> _availableTimers = new List<TimerInstance>();
    private static List<TimerInstanceViewModel> _currentlyActiveTimers = new List<TimerInstanceViewModel>();
    private static bool _timersEnabled;

    private static List<IDisposable> _showTimerSubs = new List<IDisposable>();
    private static List<IDisposable> _hideTimerSubs = new List<IDisposable>();
    private static List<IDisposable> _reorderSubs = new List<IDisposable>();
    public static event Action<TimerInstanceViewModel,Action<TimerInstanceViewModel>> TimerExpired = delegate { };
    public static event Action<TimerInstanceViewModel,Action<TimerInstanceViewModel>> TimerTriggered = delegate { };
    public static event Action ReorderRequested = delegate {  };
    public static void Init()
    {
        CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
        CombatLogStreamer.HistoricalLogsStarted += HistoricalStarted;
        CombatLogStreamer.CombatUpdated += CombatStateUpdated;
        CombatLogStreamer.NewLineStreamed += NewLogStreamed;
        DefaultTimersManager.Init();
        RefreshAvailableTimers();
    }

    private static void HistoricalStarted()
    {
        historicalParseFinished = false;
    }

    public static void RefreshAvailableTimers()
    {
        _hideTimerSubs.ForEach(s=>s.Dispose());
        _showTimerSubs.ForEach(s=>s.Dispose());
        _reorderSubs.ForEach(s=>s.Dispose());
        var allDefaults = DefaultTimersManager.GetAllDefaults();
        var timers = allDefaults.SelectMany(t => t.Timers);
        var secondaryTimers = timers.Where(t=>t.Clause1 != null).Select(t => t.Clause1);
        secondaryTimers = secondaryTimers.Concat(timers.Where(t=>t.Clause2 != null).Select(t => t.Clause2));
        var allTimers = timers.Concat(secondaryTimers);
        _availableTimers = allTimers.Select(t => new TimerInstance(t)).ToList();
        foreach (var timerInstance in _availableTimers)
        {
            if (timerInstance.SourceTimer.IsSubTimer)
            {
                var parentTimer = _availableTimers.FirstOrDefault(t => t.SourceTimer.Id == timerInstance.ParentTimerId);
                if (parentTimer != null)
                {
                    timerInstance.ParentTimer = parentTimer;
                }
            }
            if (!string.IsNullOrEmpty(timerInstance.ExperiationTimerId))
            {
                var trigger = _availableTimers.FirstOrDefault(t => t.SourceTimer.Id == timerInstance.ExperiationTimerId);
                if (trigger != null)
                {
                    timerInstance.ExpirationTimer = trigger;
                }
            }
            if (!string.IsNullOrEmpty(timerInstance.CancellationTimerId)) 
            {
                var cancelTrigger = _availableTimers.FirstOrDefault(t => t.SourceTimer.Id == timerInstance.CancellationTimerId);
                if (cancelTrigger != null)
                {
                    timerInstance.CancelTimer = cancelTrigger;
                }
            }
        }

        _hideTimerSubs = _availableTimers.Select(t => Observable.FromEvent<Action<TimerInstanceViewModel,bool>,Tuple<TimerInstanceViewModel,bool>>(
            onNextHandler =>(p1, p2) => onNextHandler(Tuple.Create(p1,p2)),
            manager => t.TimerOfTypeExpired += manager,
            manager => t.TimerOfTypeExpired -= manager
        ).Subscribe(args=>OnTimerExpired(args.Item1,args.Item2))).ToList();
        _showTimerSubs = _availableTimers.Select(t =>
            Observable.FromEvent<TimerInstanceViewModel>(handler => t.NewTimerInstance += handler,
                handler => t.NewTimerInstance -= handler).Subscribe(AddTimerVisual)).ToList();
        _reorderSubs = _availableTimers.Select(t =>
            Observable.FromEvent(handler => t.ReorderRequested += handler,
                handler => t.ReorderRequested -= handler).Subscribe(_=>ReorderRequest())).ToList();
    }

    public static List<TimerInstanceViewModel> GetActiveTimers()
    {
        lock (_currentTimersModLock)
        {
            return _currentlyActiveTimers;
        }
    }

    private static void ReorderRequest()
    {
        ReorderRequested();
    }
    private static void AddTimerVisual(TimerInstanceViewModel t)
    {
        TimerTriggered(t,TimerAddedCallback);
    }

    private static object _currentTimersModLock = new object();
    private static void TimerAddedCallback(TimerInstanceViewModel addedTimer)
    {
        lock (_currentTimersModLock)
        {
            if (_currentlyActiveTimers.Any(t => t.SourceTimer.Id == addedTimer.SourceTimer.Id))
                return;
            _currentlyActiveTimers.Add(addedTimer);
        }

    }
    private static void OnTimerExpired(TimerInstanceViewModel t, bool endedNatrually)
    {
        TimerExpired(t,TimerRemovedCallback);
    }
    private static void TimerRemovedCallback(TimerInstanceViewModel removedTimer)
    {
        lock (_currentTimersModLock)
        {
            _currentlyActiveTimers.Remove(removedTimer);
        }
    }
    private static void EnableTimers(DateTime combatEndTime, bool localPlayerIdentified)
    {
        historicalParseFinished = true;
        _timersEnabled = true;
    }

    private static void NewLogStreamed(ParsedLogEntry log)
    {
        var encounter = CombatLogStateBuilder.CurrentState.GetEncounterActiveAtTime(log.TimeStamp);
        var bossData = CombatIdentifier.GetCurrentBossInfo(new List<ParsedLogEntry>() { log }, encounter);
        _currentDiscipline ??= CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(log.TimeStamp).Discipline;
        foreach (var timer in _availableTimers)
        {
            if (!timer.TrackOutsideOfCombat && !CombatDetector.InCombat)
                continue;
            timer.CheckForTrigger(log, _startTime, _currentDiscipline, _currentlyActiveTimers.ToList(), encounter,bossData);
        }
    }

    private static void CombatStateUpdated(CombatStatusUpdate obj)
    {
        if (!historicalParseFinished)
            return;
        if (obj.Type == UpdateType.Start)
        {
            VariableManager.ResetVariables();
            _startTime = obj.CombatStartTime;
            UncancellBeforeCombat();
            _currentDiscipline =  CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(obj.CombatStartTime).Discipline;
        }
        if (obj.Type == UpdateType.Stop)
        {
            CancelAfterCombat();
        }
    }
    private static void UncancellBeforeCombat()
    {
        foreach (var timer in _availableTimers)
        {
            timer.UnCancel();
        }
    }
    private static void CancelAfterCombat()
    {
        foreach (var timer in _availableTimers.Where(t=>!t.SourceTimer.TrackOutsideOfCombat))
        {
            timer.Cancel();
        }
    }
}