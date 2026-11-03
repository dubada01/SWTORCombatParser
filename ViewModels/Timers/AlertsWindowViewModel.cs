using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveUI;
using SWTORCombatParser.Views.Timers;

namespace SWTORCombatParser.ViewModels.Timers;

public class AlertsWindowViewModel : TimersWindowViewModel
{

    private List<TimerInstanceViewModel> _currentTimers = new List<TimerInstanceViewModel>();
    public override bool ShouldBeVisible => _alertPlaying;
    public List<TimerInstanceViewModel> SwtorTimers
    {
        get => _swtorTimers;
        set => this.RaiseAndSetIfChanged(ref _swtorTimers, value);
    }

    public AlertsWindowViewModel(string overlayName) : base(overlayName)
    {
        TimerController.TimerExpired += RemoveTimer;
        TimerController.TimerTriggered += AddTimerVisual;
        MainContent = new AlertView(this);
    }
    
    private object _timerChangeLock = new object();
    private List<TimerInstanceViewModel> _swtorTimers = new List<TimerInstanceViewModel>();
    private bool _alertPlaying;

    protected override void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
    {
        if (obj.SourceTimer.IsHot || !Active || !obj.SourceTimer.IsAlert || obj.SourceTimer.IsSubTimer)
        {
            callback(obj);
            return;
        }

        _alertPlaying = true;
        ShowOverlayWindow();

        lock (_timerChangeLock)
        {
            if(_currentTimers.Any(t => t.SourceTimer.Id == obj.SourceTimer.Id))
            {
                callback(obj);
                return;
            }
            _currentTimers.Add(obj);
        }
        SwtorTimers = new List<TimerInstanceViewModel>(_currentTimers.OrderBy(t => t.TimerValue));
        callback(obj);
    }

    protected override void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
    {
        lock (_timerChangeLock)
        {
            _currentTimers.Remove(removedTimer);
        }
        SwtorTimers = new List<TimerInstanceViewModel>(_currentTimers.OrderBy(t => t?.TimerValue ?? 0));
        callback(removedTimer);
        if (SwtorTimers.Count == 0)
        {
            HideOverlayWindow();
            _alertPlaying = false;
            UpdateVisibility();
        }
    }

    protected override void ReorderTimers(string id)
    {
        
    }
}