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
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidHotCell : INotifyPropertyChanged
    {
        private string name;

        public int Row { get; set; }
        public int Column { get; set; }
        public string Name
        {
            get => name; set
            { 
                name = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<TimerInstanceViewModel> RaidHotsOnPlayer { get; set; } = new ObservableCollection<TimerInstanceViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;

        private void RemoveFromList(TimerInstanceViewModel obj)
        {
            Application.Current.Dispatcher.Invoke(() => {
                RaidHotsOnPlayer.Remove(obj);
            });
        }

        internal void AddTimer(TimerInstanceViewModel obj)
        {
            Application.Current.Dispatcher.Invoke(() => {
                RaidHotsOnPlayer.Add(obj);
            });

            obj.TimerExpired += RemoveFromList;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
