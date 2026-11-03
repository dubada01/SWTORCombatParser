using SWTORCombatParser.Model.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private ObservableCollection<TimerInstanceViewModel> _swtorTimers = new ObservableCollection<TimerInstanceViewModel>();
        public ObservableCollection<TimerInstanceViewModel> SwtorTimers
        {
            get => _swtorTimers;
            set => this.RaiseAndSetIfChanged(ref _swtorTimers, value);
        }
        public string TimerTitle
        {
            get => _timerTitle;
            set => this.RaiseAndSetIfChanged(ref _timerTitle, value);
        }

        protected List<TimerInstanceViewModel> _visibleTimers = new List<TimerInstanceViewModel>();

        public TimersWindowViewModel(string overlayName) : base(overlayName)
        {
            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
        }
        public void SetScale(double scale)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var timer in SwtorTimers)
                {
                    timer.Scale = scale;
                }
            });
        }
        protected abstract void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback);

        protected abstract void RemoveTimer(TimerInstanceViewModel removedTimer,
            Action<TimerInstanceViewModel> callback);

        protected abstract void ReorderTimers(string id);
    }
}
