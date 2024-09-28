using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;

namespace SWTORCombatParser.ViewModels.Timers;

public class AlertsWindowViewModel : BaseOverlayViewModel, INotifyPropertyChanged
{

    private List<TimerInstanceViewModel> _currentTimers = new List<TimerInstanceViewModel>();
    public List<TimerInstanceViewModel> SwtorTimers { get; set; } =
        new List<TimerInstanceViewModel>();

    public AlertsWindowViewModel()
    {
        _overlayWindow = new AlertView(this);
        TimerController.TimerExpired += RefreshTimerVisuals;
        TimerController.TimerTriggered += AddTimerVisual;
        Dispatcher.UIThread.Invoke(() =>
        {
            var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType("Alerts");
            _active = defaultTimersInfo.Acive;
            _overlayWindow.Position = new PixelPoint((int)defaultTimersInfo.Position.X, (int)defaultTimersInfo.Position.Y);
            _overlayWindow.Width = defaultTimersInfo.WidtHHeight.X;
            _overlayWindow.Height = defaultTimersInfo.WidtHHeight.Y;
        });
    }
    
    private object _timerChangeLock = new object();
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
        OnPropertyChanged("SwtorTimers");
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
        OnPropertyChanged("SwtorTimers");
    }
}