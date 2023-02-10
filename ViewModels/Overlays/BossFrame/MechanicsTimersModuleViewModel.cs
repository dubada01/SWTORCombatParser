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
    public class MechanicsTimersModuleViewModel : INotifyPropertyChanged
    {
        private EntityInfo _bossInfo;
        private bool isActive;
        private object timerLock = new object();
        public ObservableCollection<TimerInstanceViewModel> UpcomingMechanics { get; set; } = new ObservableCollection<TimerInstanceViewModel>();
        public MechanicsTimersModuleViewModel(EntityInfo bossInfo, bool mechTrackingEnabled)
        {
            isActive = mechTrackingEnabled;
            _bossInfo = bossInfo;
            TimerController.TimerTriggered += OnNewTimer;
            TimerController.TimerExpired += RemoveTimer;
        }
        public void SetActive(bool state)
        {
            isActive = state;
        }
        private void OnNewTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            if(!isActive)
                return;
            lock (timerLock)
            {
                if (obj.SourceTimer.IsMechanic && obj.SourceTimer.TriggerType == TimerKeyType.EntityHP && !obj.SourceTimer.IsSubTimer)
                {
                    var unorderedUpcomingMechs = UpcomingMechanics.ToList();
                    unorderedUpcomingMechs.Add(obj);
                    var ordered = unorderedUpcomingMechs.OrderByDescending(t =>t.SourceTimer.HPPercentage);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        UpcomingMechanics = new ObservableCollection<TimerInstanceViewModel>(ordered);
                        OnPropertyChanged("UpcomingMechanics");
                    });
                }
                callback(obj);
            }
        }
        
        private void RemoveTimer(TimerInstanceViewModel obj, Action<TimerInstanceViewModel> callback)
        {
            lock (timerLock)
            {
                App.Current.Dispatcher.Invoke(() => { UpcomingMechanics.Remove(obj); });
                callback(obj);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
