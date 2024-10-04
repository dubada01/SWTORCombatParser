using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Threading;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class DisciplineTimersWindowViewModel : TimersWindowViewModel
    {
        private string _timerSource;
        private bool _timersEnabled;
        private List<TimerInstance> _activeTimers = new List<TimerInstance>();
        public DisciplineTimersWindowViewModel()
        {
            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
            this.OverlayName = "Timers";
            _timerWindow = new TimersWindow(this);
            _timerWindow.SetIdText("DISCIPLINE TIMERS");
        }
        private void UpdateSource()
        {
            if (_timerSource.Contains('|') || _timerSource == "Shared" || _timerSource == "HOTS")
                return;
            TimerTitle = _timerSource + " Timers";
            SwtorTimers = new List<TimerInstanceViewModel>();
            _timerWindow.SetPlayer(_timerSource);
            Dispatcher.UIThread.Invoke(() =>
            {
                var defaultTimersInfo = DefaultOrbsTimersManager.GetDefaults(_timerSource);
                _timerWindow.Position = new PixelPoint((int)defaultTimersInfo.Position.X, (int)defaultTimersInfo.Position.Y);
                _timerWindow.Width = defaultTimersInfo.WidtHHeight.X;
                _timerWindow.Height = defaultTimersInfo.WidtHHeight.Y;
                if(OverlaysMoveable)
                    ShowOverlayWindow();
                else
                {
                    HideOverlayWindow();
                }
            });
        }
        private object _timerChangeLock = new object();
        private double _currentScale;

        private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (obj.SourceTimer.IsHot || !Active || obj.SourceTimer.IsMechanic || obj.SourceTimer.IsAlert || obj.SourceTimer.IsBuiltInDefensive || obj.TimerValue <= 0)
            {
                callback(obj);
                return;
            }
            obj.Scale = _currentScale;
            lock (_timerChangeLock)
            {
                _visibleTimers.Add(obj);
            }
            ReorderTimers();
            callback(obj);
        }

        private void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
            }
            ReorderTimers();
            callback(removedTimer);
        }
        private void ReorderTimers()
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.RemoveAll(t => t.TimerValue < 0);
                SwtorTimers = new List<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            }
        }
    }
}
