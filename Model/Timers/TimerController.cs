using System;
using System.Collections.Generic;
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
    private static bool _timersEnabled;
    public static event Action<TimerInstanceViewModel> TimerExpired = delegate { };
    public static event Action<TimerInstanceViewModel> TimerTiggered = delegate { };
    public static void Init()
    {
        CombatLogStreamer.HistoricalLogsFinished += EnableTimers;
        CombatLogStreamer.CombatUpdated += CombatStateUpdated;
        CombatLogStreamer.NewLineStreamed += NewLogStreamed;
        RefreshAvailableTimers();
    }
    
    public static void RefreshAvailableTimers()
    {
        _availableTimers.ForEach(t => t.TimerOfTypeExpired -= OnTimerExpired);
        _availableTimers.ForEach(t => t.NewTimerInstance -= AddTimerVisual);
        var timers = DefaultTimersManager.GetAllDefaults().SelectMany(t => t.Timers);
        _availableTimers = timers.Select(t => new TimerInstance(t)).ToList();
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
        TimerTiggered(t);
    }

    private static void OnTimerExpired(TimerInstanceViewModel t)
    {
        TimerExpired(t);
    }
    private static void EnableTimers(DateTime combatEndTime, bool localPlayerIdentified)
    {
        _timersEnabled = true;
    }
    private static void NewLogStreamed(ParsedLogEntry log)
    {            
        Parallel.ForEach(_availableTimers, timer =>
        {
            if(!timer.TrackOutsideOfCombat && !CombatDetector.InCombat)
                return;
            timer.CheckForTrigger(log, DateTime.Now);
        });
    }

    private static void CombatStateUpdated(CombatStatusUpdate obj)
    {
        if (obj.Type == UpdateType.Start)
        {
            UncancellBeforeCombat();
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