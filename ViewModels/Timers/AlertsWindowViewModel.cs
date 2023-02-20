using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SWTORCombatParser.DataStructures;
using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.DataStructures.EncounterInfo;
using SWTORCombatParser.Model.CombatParsing;
using SWTORCombatParser.Model.LogParsing;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;

namespace SWTORCombatParser.ViewModels.Timers;

public class AlertsWindowViewModel:INotifyPropertyChanged
{
    private ITimerWindow _timerWindow;
    private bool active;

    public event Action CloseRequested = delegate { };
    public event Action<bool> OnLocking = delegate { };
    public event Action<string> OnCharacterDetected = delegate { };
    public event PropertyChangedEventHandler PropertyChanged;

    public bool OverlaysMoveable { get; set; }
    private List<TimerInstanceViewModel> _currentTimers = new List<TimerInstanceViewModel>();
    public List<TimerInstanceViewModel> SwtorTimers { get; set; } =
        new List<TimerInstanceViewModel>();

    public bool Active
    {
        get => active;
        set
        {
            active = value;
            if (!active)
            {
                HideTimers();
            }
            else
            {
                if (OverlaysMoveable)
                {
                    _timerWindow.Show();
                }
            }

        }
    }

    public AlertsWindowViewModel()
    {
        _timerWindow = new AlertView(this);
        TimerController.TimerExpired += RefreshTimerVisuals;
        TimerController.TimerTriggered += AddTimerVisual;
        App.Current.Dispatcher.Invoke(() =>
        {
            var defaultTimersInfo = DefaultGlobalOverlays.GetOverlayInfoForType("Alerts");
            active = defaultTimersInfo.Acive;
            _timerWindow.Top = defaultTimersInfo.Position.Y;
            _timerWindow.Left = defaultTimersInfo.Position.X;
            _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
            _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
        });
    }

    public void ShowTimers()
    {
        if (!Active)
            return;
        App.Current.Dispatcher.Invoke(() =>
        {
            _timerWindow.Show();
        });
    }

    public void HideTimers()
    {
        App.Current.Dispatcher.Invoke(() => { _timerWindow.Hide(); });
    }
    private object _timerChangeLock = new object();
    private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
    {
        if (obj.SourceTimer.IsHot || !Active || !obj.SourceTimer.IsAlert || obj.SourceTimer.IsSubTimer)
        {
            callback(obj);
            return;
        }
        ShowTimers();

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
            HideTimers();
        OnPropertyChanged("SwtorTimers");
    }

    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal void UpdateLock(bool value)
    {
        OverlaysMoveable = !value;
        OnPropertyChanged("OverlaysMoveable");
        OnLocking(value);
    }
    
}