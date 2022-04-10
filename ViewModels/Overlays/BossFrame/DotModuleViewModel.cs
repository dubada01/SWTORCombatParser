using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays.BossFrame
{
    public class DotModuleViewModel : INotifyPropertyChanged
    {
        private EntityInfo _bossInfo;
        public ObservableCollection<TimerInstanceViewModel> ActiveDOTS { get; set; } = new ObservableCollection<TimerInstanceViewModel>();

        public DotModuleViewModel(EntityInfo bossInfo)
        {
            _bossInfo = bossInfo;
            TimerNotifier.NewTimerTriggered += OnNewTimer;
        }

        private void OnNewTimer(TimerInstanceViewModel obj)
        {
            if (obj.TargetAddendem == _bossInfo.Entity.Name)
            {
                obj.TimerExpired += RemoveTimer;
                App.Current.Dispatcher.Invoke(() =>
                {
                    ActiveDOTS.Add(obj);
                });
            }
        }

        private void RemoveTimer(TimerInstanceViewModel obj)
        {
            App.Current.Dispatcher.Invoke(() => {
                ActiveDOTS.Remove(obj);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
