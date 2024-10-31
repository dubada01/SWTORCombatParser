using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Timers
{
    public class DisciplineTimersWindowViewModel : TimersWindowViewModel
    {
        private string _timerSource;
        private bool _timersEnabled;
        public override bool ShouldBeVisible => true;
        public DisciplineTimersWindowViewModel(string overlayName) : base(overlayName)
        {
            MainContent = new TimersWindow(this);
            _timerWindow = new BaseOverlayWindow(this);
        }

        private object _timerChangeLock = new object();
        private double _currentScale;

        protected override void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
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

        protected override void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
            }
            ReorderTimers();
            callback(removedTimer);
        }
        protected override void ReorderTimers()
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.RemoveAll(t => t.TimerValue < 0);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            }
        }
    }
}
