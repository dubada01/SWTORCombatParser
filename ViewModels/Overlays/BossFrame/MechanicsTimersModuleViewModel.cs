using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using ReactiveUI;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class MechanicsTimersModuleViewModel : ReactiveObject
    {
        private EntityInfo _bossInfo;
        private bool isActive = true;
        private object timerLock = new object();
        private double _currentScale;
        private ObservableCollection<TimerInstanceViewModel> _upcomingMechanics = new();

        public ObservableCollection<TimerInstanceViewModel> UpcomingMechanics
        {
            get => _upcomingMechanics;
            set => this.RaiseAndSetIfChanged(ref _upcomingMechanics, value);
        }

        public MechanicsTimersModuleViewModel(EntityInfo bossInfo, double scale)
        {
            _currentScale = scale;
            _bossInfo = bossInfo;
            TimerController.TimerTriggered += OnNewTimer;
            TimerController.TimerExpired += RemoveTimer;
        }

        public void SetScale(double scale)
        {
            _currentScale = scale;
            Dispatcher.UIThread.Invoke(() =>
            {
                foreach (var timer in UpcomingMechanics)
                {
                    timer.Scale = scale;
                }
            });
        }
        private void OnNewTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if (!isActive)
                return;

            if (obj.SourceTimer.IsMechanic && (obj.SourceTimer.TriggerType == TimerKeyType.EntityHP || obj.SourceTimer.TriggerType == TimerKeyType.AbsorbShield) && !obj.SourceTimer.IsSubTimer && _bossInfo.Entity.Id == obj.TargetId)
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    var unorderedUpcomingMechs = UpcomingMechanics.ToList();
                    obj.Scale = _currentScale * 1.25d;
                    unorderedUpcomingMechs.Add(obj);
                    var ordered = unorderedUpcomingMechs.OrderByDescending(t => t.SourceTimer.HPPercentage);

                    UpcomingMechanics = new ObservableCollection<TimerInstanceViewModel>(ordered);
                });
            }
            callback(obj);

        }

        private void RemoveTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {

            Dispatcher.UIThread.Invoke(() =>
            {
                UpcomingMechanics.Remove(obj);
            });
            callback(obj);

        }
    }
}
