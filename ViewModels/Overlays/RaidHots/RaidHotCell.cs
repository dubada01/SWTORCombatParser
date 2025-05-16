using SWTORCombatParser.ViewModels.Timers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using SWTORCombatParser.Utilities;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidHotCell : INotifyPropertyChanged
    {
        private string name;
        private bool usingSubtleHOTView;
        private double nameOpacity = 1;
        private List<int> namePixelLocation = new List<int>();
        private double ratioChangeThreshold = 150;
        private List<List<int>> _namePixelsIndeciesHistory = new List<List<int>>();
        private int column;
        private bool isTargeted;
        private HorizontalAlignment dCDHorAlignment;

        public int Row { get; set; }
        public HorizontalAlignment DCDHorAlignment
        {
            get => dCDHorAlignment; set
            {
                dCDHorAlignment = value;
                OnPropertyChanged();
            }
        }
        public int Column
        {
            get => column; set
            {
                column = value;
                if (column == 0)
                {
                    HotsColumn = 1;
                    DcdsColumn = 0;
                    LeftColumnWidth = new GridLength(.25, GridUnitType.Star);
                    RightColumnWidth = new GridLength(.75, GridUnitType.Star);
                    DCDHorAlignment = HorizontalAlignment.Right;
                    return;
                }
                if (column == Columns - 1)
                {
                    HotsColumn = 0;
                    DcdsColumn = 1;
                    LeftColumnWidth = new GridLength(.75, GridUnitType.Star);
                    RightColumnWidth = new GridLength(.25, GridUnitType.Star);
                    DCDHorAlignment = HorizontalAlignment.Left;
                    return;
                }
                HotsColumn = 0;
                LeftColumnWidth = new GridLength(1, GridUnitType.Star);
                RightColumnWidth = new GridLength(0);
            }
        }
        public GridLength LeftColumnWidth { get; set; }
        public GridLength RightColumnWidth { get; set; }
        public int HotsColumn { get; set; }
        public int DcdsColumn { get; set; }
        public double NameOpacity
        {
            get => nameOpacity; set
            {
                nameOpacity = value;
                OnPropertyChanged();
            }
        }
        public void Reset()
        {
            Name = "";
            Dispatcher.UIThread.Invoke(() =>
            {
                RaidHotsOnPlayer.Clear();
                DCDSOnPlayer.Clear();
            });
            IsTargeted = false;
            TargetedBy = 0;
            NameJustChanged = true;
            StaticPixelChanges.Clear();
            StaticNamePixelIndicies.Clear();
            NamePixelIndicies.Clear();
        }
        public bool NameJustChanged { get; set; } = true;
        public int PixelIndexDiffCount { get; set; }
        public List<int> StaticNamePixelIndicies { get; set; } = new List<int>();
        public List<int> StaticPixelChanges { get; set; } = new List<int>();


        public List<int> NamePixelIndicies
        {
            get => namePixelLocation; set
            {
                NameJustChanged = _namePixelsIndeciesHistory.Count == 0 && value.Count() > 200;
                if (_namePixelsIndeciesHistory.Count() == 5)
                {
                    var newStaticPixels = GetCommonPixelIncicies();
                    if (StaticNamePixelIndicies.Count == 0)
                        StaticNamePixelIndicies = newStaticPixels;
                    StaticPixelChanges = GetDifferencesInList(newStaticPixels, StaticNamePixelIndicies);
                    PixelIndexDiffCount = StaticPixelChanges.Count;
                    NameJustChanged = PixelIndexDiffCount > ratioChangeThreshold;
                    StaticNamePixelIndicies = newStaticPixels;
                    _namePixelsIndeciesHistory.RemoveAt(0);
                }
                _namePixelsIndeciesHistory.Add(value);

                namePixelLocation = value;
            }
        }

        private List<int> GetCommonPixelIncicies()
        {
            return _namePixelsIndeciesHistory.Skip(1).Aggregate(new HashSet<int>(_namePixelsIndeciesHistory.First()),
                (h, e) =>
                {
                    h.IntersectWith(e);
                    return h;
                }).ToList();
        }
        private List<int> GetDifferencesInList(List<int> list1, List<int> list2)
        {
            var added = list1.Except(list2);
            var removed = list2.Except(list1);
            return added.Concat(removed).ToList();
        }
        public long TargetedBy { get; set; }
        //TODO make this configurable so that users can turn it off. Also, probably add different colors for when there's many bosses
        public bool IsTargeted
        {
            get => isTargeted; set
            {
                var displayTargeted = Settings.ReadSettingOfType<bool>("overlay_show_targeted");
                if (!displayTargeted && value)
                    return;
                isTargeted = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => name; set
            {
                name = value;
                OnPropertyChanged();
            }
        }

        public bool HasHOT => RaidHotsOnPlayer.Any();
        public bool UsingSubtleHOTView
        {
            get => usingSubtleHOTView; set
            {
                usingSubtleHOTView = value;
                OnPropertyChanged();
            }
        }
        public RaidHotCell(int columnCount)
        {
            Columns = columnCount;
        }
        public ObservableCollection<TimerInstanceViewModel> RaidHotsOnPlayer { get; set; } = new ObservableCollection<TimerInstanceViewModel>();

        public ObservableCollection<TimerInstanceViewModel> DCDSOnPlayer { get; set; } = new ObservableCollection<TimerInstanceViewModel>();
        public int Columns { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public bool AlreadyHasTimer(string timerName) => Dispatcher.UIThread.Invoke(() =>
                                                                  {
                                                                      return RaidHotsOnPlayer.Any(t => t.TimerName == timerName);
                                                                  });
        private void RemoveFromList(TimerInstanceViewModel obj, bool endedNatrually)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                RaidHotsOnPlayer.Remove(obj);
                DCDSOnPlayer.Remove(obj);
            });
        }
        private void RefreshList()
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                var currentHots = RaidHotsOnPlayer.OrderBy(t => t.TimerValue);
                RaidHotsOnPlayer = new ObservableCollection<TimerInstanceViewModel>(currentHots.Where(h => h.TimerValue > 0));
                var currentDcds = DCDSOnPlayer.OrderBy(t => t.TimerValue);
                DCDSOnPlayer = new ObservableCollection<TimerInstanceViewModel>(currentDcds.Where(h => h.TimerValue > 0));
                OnPropertyChanged("RaidHotsOnPlayer");
                OnPropertyChanged("DCDSOnPlayer");
            });
        }
        internal void AddHOT(TimerInstanceViewModel obj)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                RaidHotsOnPlayer.Add(obj);
            });
            obj.FireTimerStarted();
            obj.TimerExpired += RemoveFromList;
            obj.TimerRefreshed += RefreshList;
        }
        internal void AddDCD(TimerInstanceViewModel obj)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                DCDSOnPlayer.Add(obj);
            });

            obj.TimerExpired += RemoveFromList;
            obj.TimerRefreshed += RefreshList;
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
