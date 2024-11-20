using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Timers;

public class AlertsWindowViewModel : BaseOverlayViewModel
{

    private List<TimerInstanceViewModel> _currentTimers = new List<TimerInstanceViewModel>();
    public override bool ShouldBeVisible => true;
    public List<TimerInstanceViewModel> SwtorTimers
    {
        get => _swtorTimers;
        set => this.RaiseAndSetIfChanged(ref _swtorTimers, value);
    }

    public AlertsWindowViewModel(string overlayName) : base(overlayName)
    {
        TimerController.TimerExpired += RefreshTimerVisuals;
        TimerController.TimerTriggered += AddTimerVisual;
    }
    
    private object _timerChangeLock = new object();
    private List<TimerInstanceViewModel> _swtorTimers = new List<TimerInstanceViewModel>();

    private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
    {
        if (obj.SourceTimer.IsHot || !Active || !obj.SourceTimer.IsAlert || obj.SourceTimer.IsSubTimer)
        {
            callback(obj);
            return;
        }
        ShowOverlayWindow();

        lock (_timerChangeLock)
        {
            _currentTimers.Add(obj);
            SwtorTimers = new List<TimerInstanceViewModel>(_currentTimers.OrderBy(t => t.TimerValue));
            callback(obj);
        }
    }

    private void RefreshTimerVisuals(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
    {
        lock (_timerChangeLock)
        {
            _currentTimers.Remove(removedTimer);
            SwtorTimers = new List<TimerInstanceViewModel>(_currentTimers.OrderBy(t => t.TimerValue));
            callback(removedTimer);
        }
        if (SwtorTimers.Count == 0)
            HideOverlayWindow();
    }
}