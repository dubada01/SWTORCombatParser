using MoreLinq;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SWTORCombatParser.ViewModels.Overlays.RaidHots
{
    public class RaidFrameOverlayViewModel : INotifyPropertyChanged
    {
        private int rows;
        private int columns;
        private bool editable;
        private bool active;
        private RaidFrameOverlay _view;
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
        public bool Active
        {
            get => active; set
            {
                active = value;
                //if (active)
                //    CheckForUpdatedName();
            }
        }
        public System.Drawing.Point TopLeft { get; set; }
        public int Width { get; set; }
        public double ScreenWidth { get; set; }
        public double ColumnWidth => ScreenWidth / Columns;
        public int Height { get; set; }
        public double ScreenHeight { get; set; }
        public double RowHeight => ScreenHeight / Rows;

        public List<PlacedName> CurrentNames = new List<PlacedName>();

        public event Action NamesUpdated = delegate { };
        public event Action<bool> ToggleLocked = delegate { };
        public event Action<string> PlayerChanged = delegate { };
        public RaidFrameOverlayViewModel(RaidFrameOverlay view)
        {
            _view = view;
            TimerNotifier.NewTimerTriggered += CheckForRaidHOT;
            //CheckForUpdatedName();
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj)
        {
            if (obj.SourceTimer.IsHot && CurrentNames.Any())
            {
                var playerName = obj.TargetAddendem.ToLower();
                var maxMatchingCharacters = CurrentNames.Select(n => n.Name.ToLower()).MinBy(s => LevenshteinDistance.Compute(s, playerName)).First();
                var match = LevenshteinDistance.Compute(maxMatchingCharacters, playerName);
                if (match > 3)
                    return;
                var cellToUpdate = RaidHotCells.First(c => c.Name.ToLower() == maxMatchingCharacters);
                cellToUpdate.AddTimer(obj);
            }
        }
        public void UpdateNames(List<PlacedName> orderedNames)
        {
            if (CurrentNames.Any())
            {
                List<PlacedName> namesToUpdate = new List<PlacedName>();
                foreach(var name in orderedNames)
                {
                    var bestPreviousName = CurrentNames.MinBy(s => LevenshteinDistance.Compute(s.Name.ToLower(), name.Name.ToLower())).First();
                    var match = LevenshteinDistance.Compute(bestPreviousName.Name.ToLower(), name.Name.ToLower());
                    if (match <= 3)
                    {
                        if (bestPreviousName.Row == name.Row && bestPreviousName.Column == name.Column)
                            continue;
                        bestPreviousName.Row = name.Row;
                        bestPreviousName.Column = name.Column;
                        bestPreviousName.Vertices = name.Vertices;
                        namesToUpdate.Add(bestPreviousName);
                    }
                }
                CurrentNames.Clear();
                CurrentNames.AddRange(namesToUpdate);
                CurrentNames.AddRange(orderedNames.Where(n=>!namesToUpdate.Any(un => LevenshteinDistance.Compute(n.Name.ToLower(),un.Name.ToLower())<=3)));    
            }
            else
            {
                CurrentNames = orderedNames;
            }
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
        public ICommand RefreshLayoutCommand => new CommandHandler(RefreshLayout);

        private void RefreshLayout(object obj)
        {
            NamesUpdated();
        }

        private void UpdateCells()
        {
            if (!CurrentNames.Any())
            {
                InitRaidCells();
            }
            else
            {
                foreach (var detectedName in CurrentNames)
                {
                    var cellForName = RaidHotCells.FirstOrDefault(c => c.Name == detectedName.Name);
                    var cellToReplace = RaidHotCells.First(c => c.Row == detectedName.Row && c.Column == detectedName.Column);
                    //if (!CurrentNames.Any(c => c.Name == cellToReplace.Name))
                    //{
                    //    RaidHotCells.Remove(cellToReplace);
                    //    RaidHotCells.Add(new RaidHotCell { Column = cellToReplace.Column, Row = cellToReplace.Row, Name = "" });
                    //}

                    if (cellForName != null)
                    {
                        cellForName.Column = detectedName.Column;
                        cellForName.Row = detectedName.Row;
                        if (!CurrentNames.Any(c => c.Name == cellToReplace.Name))
                        {
                            RaidHotCells.Remove(cellToReplace);
                            RaidHotCells.Add(new RaidHotCell { Column = cellToReplace.Column, Row = cellToReplace.Row, Name = "" });
                        }
                    }
                    else
                    {
                        if (!CurrentNames.Any(c => c.Name == cellToReplace.Name))
                        {
                            RaidHotCells.Remove(cellToReplace);
                        }
                        RaidHotCells.Add(new RaidHotCell { Column = detectedName.Column, Row = detectedName.Row, Name = detectedName.Name });
                    }

                }
                //for (var i = 0; i < (Rows*Columns); i++)
                //{
                //    if (!CurrentNames.Any(n => n.Name == RaidHotCells[i].Name))
                //    {
                //        var removed = RaidHotCells[i];
                //        RaidHotCells.RemoveAt(i);
                //        RaidHotCells.Add(new RaidHotCell { Column = removed.Column, Row = removed.Row, Name = "" });
                //    }
                //}
            }
            RaidHotCells =new ObservableCollection<RaidHotCell>(RaidHotCells.OrderBy(c => c.Row * Columns + c.Column));

            OnPropertyChanged("RaidHotCells");
        }

        private void InitRaidCells()
        {
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {

                    RaidHotCells.Add(new RaidHotCell { Column = c, Row = r, Name = "" });

                }
            }
        }

        public ObservableCollection<RaidHotCell> RaidHotCells { get; set; } = new ObservableCollection<RaidHotCell>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void UpdatePositionAndSize(int actualHeight, int actualWidth, double screenHeight, double screenWidth, System.Drawing.Point topLeft)
        {
            TopLeft = topLeft;
            Height = actualHeight;
            Width = actualWidth;
            ScreenHeight = screenHeight;
            ScreenWidth = screenWidth;
            OnPropertyChanged("RowHeight");
            OnPropertyChanged("ColumnWidth");
        }

        internal void FirePlayerChanged(string name)
        {
            PlayerChanged(name);

        }
    }
}
