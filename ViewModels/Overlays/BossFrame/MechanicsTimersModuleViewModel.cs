using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SWTORCombatParser.DataStructures;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class MechanicsTimersModuleViewModel : INotifyPropertyChanged
    {
        private EntityInfo _bossInfo;
        private bool isActive;
        public ObservableCollection<TimerInstanceViewModel> UpcomingMechanics { get; set; } = new ObservableCollection<TimerInstanceViewModel>();
        public MechanicsTimersModuleViewModel(EntityInfo bossInfo, bool mechTrackingEnabled)
        {
            isActive = mechTrackingEnabled;
            _bossInfo = bossInfo;
            TimerNotifier.NewTimerTriggered += OnNewTimer;
        }
        public void SetActive(bool state)
        {
            isActive = state;
        }
        private void OnNewTimer(TimerInstanceViewModel obj)
        {
            if(!isActive)
                return;
            if ((obj.SourceTimer.Source == _bossInfo.Entity.Name || (obj.SourceTimer.TriggerType == DataStructures.TimerKeyType.EntityHP && obj.TargetAddendem == _bossInfo.Entity.Name)) && obj.SourceTimer.IsMechanic)
            {
                obj.TimerExpired += RemoveTimer;
                App.Current.Dispatcher.Invoke(() =>
                {
                    UpcomingMechanics.Add(obj);
                });
            }
        }

        private void RemoveTimer(TimerInstanceViewModel obj)
        {
            App.Current.Dispatcher.Invoke(() => {
                UpcomingMechanics.Remove(obj);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
