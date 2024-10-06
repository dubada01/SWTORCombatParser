using SWTORCombatParser.DataStructures.ClassInfos;
using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Threading;
using ReactiveUI;
using SWTORCombatParser.Views;

namespace SWTORCombatParser.ViewModels.Timers
{
    public abstract class TimersWindowViewModel : BaseOverlayViewModel
    {
        private string _timerSource;
        internal BaseOverlayWindow _timerWindow;
        private string _timerTitle = "Default Title";
        private List<TimerInstance> _activeTimers = new List<TimerInstance>();

        public List<TimerInstanceViewModel> SwtorTimers
        {
            get => _swtorTimers;
            set => this.RaiseAndSetIfChanged(ref _swtorTimers, value);
        }
        public string TimerTitle
        {
            get => _timerTitle;
            set => this.RaiseAndSetIfChanged(ref _timerTitle, value);
        }

        public List<TimerInstanceViewModel> _visibleTimers = new List<TimerInstanceViewModel>();

        public TimersWindowViewModel(string overlayName) : base(overlayName)
        {
            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
        }
        public void SetScale(double scale)
        {
            _currentScale = scale;
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var timer in SwtorTimers)
                {
                    timer.Scale = scale;
                }
            });
        }
        public void SetSource(string sourceName)
        {
            if (_timerSource == sourceName)
                return;
            _timerSource = sourceName;
            UpdateSource();
        }
        public void SetPlayer(SWTORClass swtorclass)
        {
            if (_timerSource == swtorclass.Discipline)
                return;
            _timerSource = swtorclass.Discipline;
            UpdateSource();
        }
        private void UpdateSource()
        {
            if (_timerSource.Contains('|') || _timerSource == "Shared" || _timerSource == "HOTS")
                return;
            TimerTitle = _timerSource + " Timers";
            SwtorTimers = new List<TimerInstanceViewModel>();
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
        private List<TimerInstanceViewModel> _swtorTimers = new List<TimerInstanceViewModel>();

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
