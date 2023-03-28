using System;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class DotModuleViewModel : INotifyPropertyChanged
    {
        private EntityInfo _bossInfo;
        private bool isActive;
        private double _currentScale;

        public ObservableCollection<TimerInstanceViewModel> ActiveDOTS { get; set; } = new ObservableCollection<TimerInstanceViewModel>();

        public DotModuleViewModel(EntityInfo bossInfo, bool dotTrackingEnabled, double scale)
        {
            _currentScale = scale;
            isActive = dotTrackingEnabled;
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
            App.Current.Dispatcher?.Invoke(() =>
            {
                foreach (var timer in ActiveDOTS)
                {
                    timer.Scale = scale;
                }
            });
        }
        private void RemoveTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            App.Current.Dispatcher.Invoke(() => {
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
                App.Current.Dispatcher.Invoke(() => {
                    obj.Scale = _currentScale;
                    ActiveDOTS.Add(obj); 
                });
            }
            callback(obj);
        }

        private void ReorderTimers()
        {
            var currentTimers = ActiveDOTS.OrderBy(v => v.TimerValue);
            ActiveDOTS = new ObservableCollection<TimerInstanceViewModel>(currentTimers);
            OnPropertyChanged("ActiveDOTS");
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
