//using MoreLinq;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj)
        {
            if (obj.SourceTimer.IsHot && CurrentNames.Any() && obj.TargetAddendem != null)
            {
                var playerName = obj.TargetAddendem.ToLower();
                var maxMatchingCharacters = CurrentNames.Select(n => n.Name.ToLower()).MinBy(s => LevenshteinDistance.Compute(s, playerName));
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
                RaidHotCells.RemoveAll(v => !CurrentNames.Any(c=>c.Name.ToLower() == v.Name.ToLower()));
                foreach (var detectedName in CurrentNames)
                {
                    var cellForName = RaidHotCells.FirstOrDefault(c => c.Name.ToLower() == detectedName.Name.ToLower());
                    if (cellForName != null)
                    {
                        cellForName.Column = detectedName.Column;
                        cellForName.Row = detectedName.Row;
                    }
                    else
                    {
                        RaidHotCells.Add(new RaidHotCell { Column = detectedName.Column, Row = detectedName.Row, Name = detectedName.Name });
                    }

                }
            }
            FillInRaidCells();
            RaidHotCells = RaidHotCells.OrderBy(c => c.Row * Columns + c.Column).ToList();

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
        private void FillInRaidCells()
        {
            for (var r = 0; r < Rows; r++)
            {
                for (var c = 0; c < Columns; c++)
                {
                    if(!RaidHotCells.Any(v=>v.Row == r && v.Column == c))
                        RaidHotCells.Add(new RaidHotCell { Column = c, Row = r, Name = "" });

                }
            }
        }
        public List<RaidHotCell> RaidHotCells { get; set; } = new List<RaidHotCell>();

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
