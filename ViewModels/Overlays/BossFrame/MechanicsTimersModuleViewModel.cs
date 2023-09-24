using SWTORCombatParser.DataStructures;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class MechanicsTimersModuleViewModel : INotifyPropertyChanged
    {
        private EntityInfo _bossInfo;
        private bool isActive;
        private object timerLock = new object();
        private double _currentScale;

        public ObservableCollection<TimerInstanceViewModel> UpcomingMechanics { get; set; } = new ObservableCollection<TimerInstanceViewModel>();
        public MechanicsTimersModuleViewModel(EntityInfo bossInfo, bool mechTrackingEnabled, double scale)
        {
            _currentScale = scale;
            isActive = mechTrackingEnabled;
            _bossInfo = bossInfo;
            TimerController.TimerTriggered += OnNewTimer;
            TimerController.TimerExpired += RemoveTimer;
        }
        public void SetActive(bool state)
        {
            isActive = state;
        }

        public void SetScale(double scale)
        {
            _currentScale = scale;
            App.Current.Dispatcher?.Invoke(() =>
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
                App.Current.Dispatcher.Invoke(() =>
                {
                    var unorderedUpcomingMechs = UpcomingMechanics.ToList();
                    obj.Scale = _currentScale * 1.25d;
                    unorderedUpcomingMechs.Add(obj);
                    var ordered = unorderedUpcomingMechs.OrderByDescending(t => t.SourceTimer.HPPercentage);

                    UpcomingMechanics = new ObservableCollection<TimerInstanceViewModel>(ordered);
                    OnPropertyChanged("UpcomingMechanics");
                });
            }
            callback(obj);

        }

        private void RemoveTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {

            App.Current.Dispatcher.Invoke(() =>
            {
                UpcomingMechanics.Remove(obj);
            });
            callback(obj);

        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
