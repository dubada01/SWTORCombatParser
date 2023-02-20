using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.ViewModels.Timers;

namespace SWTORCombatParser.Model.Timers;

public static class TimerController
{
    private static List<TimerInstance> _availableTimers = new List<TimerInstance>();
    private static List<TimerInstanceViewModel> _currentlyActiveTimers = new List<TimerInstanceViewModel>();
    private static bool _timersEnabled;
    public static event Action<TimerInstanceViewModel> TimerExpired = delegate { };
    public static event Action<TimerInstanceViewModel> TimerTiggered = delegate { };
    public static void Init()
    {
        CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
        CombatLogStreamer.CombatUpdated += CombatStateUpdated;
        CombatLogStreamer.NewLineStreamed += NewLogStreamed;
        DefaultTimersManager.Init();
        RefreshAvailableTimers();
    }
    
    public static void RefreshAvailableTimers()
    {
        _availableTimers.ForEach(t => t.TimerOfTypeExpired -= OnTimerExpired);
        _availableTimers.ForEach(t => t.NewTimerInstance -= AddTimerVisual);
        var allDefaults = DefaultTimersManager.GetAllDefaults();
        var timers = allDefaults.SelectMany(t => t.Timers);
        var secondaryTimers = timers.Where(t=>t.Clause1 != null).Select(t => t.Clause1);
        secondaryTimers = secondaryTimers.Concat(timers.Where(t=>t.Clause2 != null).Select(t => t.Clause2));
        var allTimers = timers.Concat(secondaryTimers);
        _availableTimers = allTimers.Select(t => new TimerInstance(t)).ToList();
        foreach (var timerInstance in _availableTimers)
        {
            if (string.IsNullOrEmpty(timerInstance.ExperiationTimerId)) continue;
            var trigger = _availableTimers.First(t => t.SourceTimer.Id == timerInstance.ExperiationTimerId);
            timerInstance.ExpirationTimer = trigger;
        }
        _availableTimers.ForEach(t => t.TimerOfTypeExpired += OnTimerExpired);
        _availableTimers.ForEach(t => t.NewTimerInstance += AddTimerVisual);
    }

    private static void AddTimerVisual(TimerInstanceViewModel t)
    {
        if(!t.SourceTimer.IsSubTimer)
            TimerTiggered(t);
        _currentlyActiveTimers.Add(t);
    }

    private static void OnTimerExpired(TimerInstanceViewModel t)
    {
        if(!t.SourceTimer.IsSubTimer)
            TimerExpired(t);
        _currentlyActiveTimers.Remove(t);
    }
    private static void EnableTimers(DateTime combatEndTime, bool localPlayerIdentified)
    {
        _timersEnabled = true;
    }

    private static DateTime _startTime;
    private static string _currentDiscipline;
    private static object _logReadObject = new object();
    private static void NewLogStreamed(ParsedLogEntry log)
    {
        lock (_logReadObject)
        {
            if(_currentDiscipline == null)
                _currentDiscipline = CombatLogStateBuilder.CurrentState.GetLocalPlayerClassAtTime(log.TimeStamp).Discipline;
            Parallel.ForEach(_availableTimers, timer =>
            {
                if(!timer.TrackOutsideOfCombat && !CombatDetector.InCombat)
                    return;
                timer.CheckForTrigger(log, _startTime,_currentDiscipline,_currentlyActiveTimers.ToList());
            });
        }
    }

    private static void CombatStateUpdated(CombatStatusUpdate obj)
    {
        if (obj.Type == UpdateType.Start)
        {
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