using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidHotCell : INotifyPropertyChanged
    {
        private string name;
        private bool usingSubtleHOTView;
        private double nameOpacity = 1;
        private List<int> namePixelLocation = new List<int>();
        private double ratioChangeThreshold = 150;

        public int Row { get; set; }
        public int Column { get; set; }
        public double NameOpacity
        {
            get => nameOpacity; set
            {
                nameOpacity = value;
                OnPropertyChanged();
            }
        }
        public bool NameJustChanged { get; set; }
        public int PixelIndexDiffCount { get; set; }
        public List<int> StaticNamePixelIndicies { get; set; } = new List<int>();
        public List<int> StaticPixelChanges { get; set; } = new List<int>();
        public List<List<int>> NamePixelIndiciesHistory = new List<List<int>>();
        public List<int> NamePixelIndicies
        {
            get => namePixelLocation; set
            {
                NameJustChanged = NamePixelIndiciesHistory.Count == 0 && value.Count() > 200;
                if (NamePixelIndiciesHistory.Count() == 5)
                {
                    var newStaticPixels = GetCommonPixelIncicies();
                    if (StaticNamePixelIndicies.Count == 0)
                        StaticNamePixelIndicies = newStaticPixels;
                    StaticPixelChanges = GetDifferencesInList(newStaticPixels, StaticNamePixelIndicies);
                    PixelIndexDiffCount = StaticPixelChanges.Count;
                    NameJustChanged = PixelIndexDiffCount > ratioChangeThreshold;
                    StaticNamePixelIndicies = newStaticPixels;
                    NamePixelIndiciesHistory.RemoveAt(0);
                }
                NamePixelIndiciesHistory.Add(value);

                namePixelLocation = value;
            }
        }

        private List<int> GetCommonPixelIncicies()
        {
            return NamePixelIndiciesHistory.Skip(1).Aggregate(new HashSet<int>(NamePixelIndiciesHistory.First()),
                (h, e) =>
                {
                    h.IntersectWith(e);
                    return h;
                }).ToList();
        }
        private List<int> GetDifferencesInList(List<int> list1, List<int> list2)
        {
            var added =  list1.Except(list2);
            var removed = list2.Except(list1);
            return added.Concat(removed).ToList();
        }
        
        public string Name
        {
            get => name; set
            {
                name = value;
                OnPropertyChanged();
            }
        }
        public bool UsingSubtleHOTView
        {
            get => usingSubtleHOTView; set
            {
                usingSubtleHOTView = value;
                OnPropertyChanged();
            }
        }
        public RaidHotCell()
        {

        }
        public ObservableCollection<TimerInstanceViewModel> RaidHotsOnPlayer { get; set; } = new ObservableCollection<TimerInstanceViewModel>();

        public event PropertyChangedEventHandler PropertyChanged;

        private void RemoveFromList(TimerInstanceViewModel obj, bool endedNatrually)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RaidHotsOnPlayer.Remove(obj);
            });
        }

        internal void AddTimer(TimerInstanceViewModel obj)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
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
