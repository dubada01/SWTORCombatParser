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
            TimerController.TimerTiggered += OnNewTimer;
        }
        public void SetActive(bool state)
        {
            isActive = state;
        }
        private void OnNewTimer(TimerInstanceViewModel obj)
        {
            if(!isActive)
                return;
            lock (timerLock)
            {
                if (obj.SourceTimer.IsMechanic)
                {
                    obj.TimerExpired += RemoveTimer;
                    var unorderedUpcomingMechs = UpcomingMechanics.ToList();
                    unorderedUpcomingMechs.Add(obj);
                    var ordered = unorderedUpcomingMechs.OrderByDescending(t =>
                        t.SourceTimer.DurationSec == 0 ? t.SourceTimer.HPPercentage : t.SourceTimer.DurationSec);
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        UpcomingMechanics = new ObservableCollection<TimerInstanceViewModel>(ordered);
                        OnPropertyChanged("UpcomingMechanics");
                    });
                }
            }
        }
        
        private void RemoveTimer(TimerInstanceViewModel obj,bool endedNatrually)
        {
            lock (timerLock)
            {
                App.Current.Dispatcher.Invoke(() => { UpcomingMechanics.Remove(obj); });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
