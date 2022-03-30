using MoreLinq;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidFrameOverlayViewModel : INotifyPropertyChanged
    {
        private int rows;
        private int columns;
        private bool editable;
        public bool Editable
        {
            get => editable;
            set
            {
                editable = value;
                ToggleLocked(!editable);
                OnPropertyChanged();
            }
        }
        public Point TopLeft { get; set; }
        public int Width { get; set; }
        public double ScreenWidth { get; set; }
        public double ColumnWidth => ScreenWidth / Columns;
        public int Height { get; set; }
        public double ScreenHeight { get; set; }
        public double RowHeight => ScreenHeight / Rows;

        public List<PlacedName> CurrentNames = new List<PlacedName>();
        public event Action<bool> ToggleLocked = delegate { };
        public RaidFrameOverlayViewModel()
        {
            TimerNotifier.NewTimerTriggered += CheckForRaidHOT;
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj)
        {
            if (obj.SourceTimer.IsHot && CurrentNames.Any())
            {
                var playerName = obj.TargetAddendem.ToLower();
                var maxMatchingCharacters = CurrentNames.Select(n=>n.Name.ToLower()).MinBy(s => LevenshteinDistance.Compute(s,playerName)).First();
                var match = LevenshteinDistance.Compute(maxMatchingCharacters, playerName);
                if (match > 3)
                    return;
                var cellToUpdate = RaidHotCells.First(c => c.Name.ToLower() == maxMatchingCharacters);
                cellToUpdate.AddTimer(obj);
            }
        }
        public void UpdateNames(List<PlacedName> orderedNames)
        {
            CurrentNames = orderedNames;
            UpdateCells();
        }
        public int Rows
        {
            get => rows; set
            {
                rows = value;
                OnPropertyChanged();
                OnPropertyChanged("RowHeight");
                UpdateCells();
            }
        }
        public int Columns
        {
            get => columns; set
            {
                columns = value;
                OnPropertyChanged();
                OnPropertyChanged("ColumnWidth");
                UpdateCells();
            }
        }
        private void UpdateCells()
        {
            RaidHotCells.Clear();
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {
                    var nameInPosition = CurrentNames.FirstOrDefault(n => n.Row == r && n.Column == c);

                    if (nameInPosition != null)
                    {
                        RaidHotCells.Add(new RaidHotCell { Column = c, Row = r, Name = nameInPosition.Name });
                    }
                    else
                    {
                        RaidHotCells.Add(new RaidHotCell { Column = c, Row = r, Name = "" });
                    }
                    
                }
            }
            OnPropertyChanged("RaidHotCells");
        }
        public ObservableCollection<RaidHotCell> RaidHotCells { get; set; } = new ObservableCollection<RaidHotCell>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void UpdatePositionAndSize(int actualHeight, int actualWidth, double screenHeight, double screenWidth, Point topLeft)
        {
            TopLeft = topLeft;
            Height = actualHeight;
            Width = actualWidth;
            ScreenHeight = screenHeight;
            ScreenWidth = screenWidth;
            OnPropertyChanged("RowHeight");
            OnPropertyChanged("ColumnWidth");
        }
    }
}
