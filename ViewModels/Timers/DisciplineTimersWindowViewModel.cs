using SWTORCombatParser.Views.Timers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

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
            ReorderTimers("Any");
            callback(obj);
        }

        protected override void RemoveTimer(TimerInstanceViewModel removedTimer, Action<TimerInstanceViewModel> callback)
        {
            lock (_timerChangeLock)
            {
                _visibleTimers.Remove(removedTimer);
            }
            ReorderTimers("Any");
            callback(removedTimer);
        }
        protected override void ReorderTimers(string id)
        {
            lock (_timerChangeLock)
            {
                if(_visibleTimers.All(t => t.SourceTimer.Id != id) && id != "Any")
                    return;
                _visibleTimers.RemoveAll(t => t.TimerValue < 0);
                SwtorTimers = new ObservableCollection<TimerInstanceViewModel>(_visibleTimers.OrderBy(t => t.TimerValue));
            }
        }
    }
}
