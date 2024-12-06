using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class DotModuleViewModel : ReactiveObject
    {
        private EntityInfo _bossInfo;
        private bool isActive;
        private double _currentScale;
        private ObservableCollection<TimerInstanceViewModel> _activeDots = new ObservableCollection<TimerInstanceViewModel>();

        public ObservableCollection<TimerInstanceViewModel> ActiveDOTS
        {
            get => _activeDots;
            set => this.RaiseAndSetIfChanged(ref _activeDots, value);
        }

        public DotModuleViewModel(EntityInfo bossInfo, double scale)
        {
            _currentScale = scale;
            isActive = true;
            _bossInfo = bossInfo;
            TimerController.TimerExpired += RemoveTimer;
            TimerController.TimerTriggered += AddTimerVisual;
            TimerController.ReorderRequested += ReorderTimers;
        }
        public void SetActive(bool state)
        {
            isActive = state;
        }
        public void SetScale(double scale)
        {
            _currentScale = scale;
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var timer in ActiveDOTS)
                {
                    timer.Scale = scale;
                }
            });
        }
        private void RemoveTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                ActiveDOTS.Remove(obj);
            });
            callback(obj);
        }

        private void AddTimerVisual(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!isActive)
                return;
            if (obj.TargetId == _bossInfo.Entity.Id && !obj.SourceTimer.IsMechanic && !obj.SourceTimer.IsSubTimer && obj.TimerValue > 0)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    obj.Scale = _currentScale;
                    ActiveDOTS.Add(obj);
                });
            }
            callback(obj);
        }

        private void ReorderTimers(string id)
        {
            if(ActiveDOTS.All(t => t.SourceTimer.Id != id))
                return;
            var currentTimers = ActiveDOTS.OrderBy(v => v.TimerValue);
            ActiveDOTS = new ObservableCollection<TimerInstanceViewModel>(currentTimers);
        }
    }
}
