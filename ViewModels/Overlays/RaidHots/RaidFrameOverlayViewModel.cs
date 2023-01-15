//using MoreLinq;
using SWTORCombatParser.Model.Overlays;
using SWTORCombatParser.Model.Timers;
using SWTORCombatParser.Utilities;
using SWTORCombatParser.ViewModels.Timers;
using SWTORCombatParser.Views.Overlay.RaidHOTs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
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
        private bool _usingDecreasedAccuracy;
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
        public bool SizeSet = false;
        public event Action<bool> ToggleLocked = delegate { };
        public event Action<string> PlayerChanged = delegate { };
        public RaidFrameOverlayViewModel(RaidFrameOverlay view)
        {
            _view = view;
            TimerController.TimerTiggered += CheckForRaidHOT;
        }
        private void CheckForRaidHOT(TimerInstanceViewModel obj)
        {
            if (!obj.SourceTimer.IsHot || !CurrentNames.Any() || obj.TargetAddendem == null) return;
            
            var playerName = obj.TargetAddendem.ToLower();
            var normalName = GetNameWithoutSpecials(playerName);
            
            var bestMatch = CurrentNames.Select(n => n.Name.ToLower()).MinBy(s => LevenshteinDistance.Compute(s, normalName));
            if (LevenshteinDistance.Compute(bestMatch, normalName) > 4 && !_usingDecreasedAccuracy)
                return;
            var cellToUpdate = RaidHotCells.FirstOrDefault(c => c.Name.ToLower() == bestMatch);
            if (cellToUpdate == null)
                return;
            cellToUpdate.AddTimer(obj);
        }

        private string GetNameWithoutSpecials(string name)
        {
            return new string(name.Normalize(NormalizationForm.FormD)
                .ToCharArray()
                .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                .ToArray()).Replace("\'","").Replace("-","");
        }
        public void UpdateNames(List<PlacedName> orderedNames)
        {
            CurrentNames = orderedNames;

            UpdateCells();
        }

        public void SetTextMatchAccuracy(bool useLowAccuracy)
        {
            _usingDecreasedAccuracy = useLowAccuracy;
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
            var workingList = RaidHotCells.ToList();
            if (!CurrentNames.Any() || RaidHotCells.Count != (Rows*Columns))
            {
                InitRaidCells();
                RaidHotCells = RaidHotCells.OrderBy(c => c.Row * Columns + c.Column).ToList();
                OnPropertyChanged("RaidHotCells");
                return;
            }
            else
            {
                //RaidHotCells.RemoveAll(v => CurrentNames.All(c => c.Name.ToLower() != v.Name.ToLower()));
                workingList.Where(v => CurrentNames.All(c => c.Name.ToLower() != v.Name.ToLower())).ToList().ForEach(c => c.Name = "");
                foreach (var detectedName in CurrentNames)
                {
                    var sourceCell = workingList.MinBy(s => LevenshteinDistance.Compute(s.Name.ToLower(), detectedName.Name.ToLower()));
                    if (sourceCell!=null && LevenshteinDistance.Compute(sourceCell.Name.ToLower(), detectedName.Name.ToLower()) <= 3)
                    {
                        var inPosCell = workingList.First(c => c.Row == detectedName.Row && c.Column == detectedName.Column);
                        var inPosCellRow = sourceCell.Row;
                        var inPosCellColumn = sourceCell.Column;

                        sourceCell.Column = detectedName.Column;
                        sourceCell.Row = detectedName.Row;
                        inPosCell.Row = inPosCellRow;
                        inPosCell.Column = inPosCellColumn;
                    }
                    else
                    {
                        var inPosCell = workingList.First(c=>c.Row== detectedName.Row && c.Column == detectedName.Column);
                        inPosCell.Name = detectedName.Name.ToUpper();
                        //RaidHotCells.Add(new RaidHotCell { Column = detectedName.Column, Row = detectedName.Row, Name = detectedName.Name.ToUpper() });
                    }
                }
            }
            //FillInRaidCells();
            RaidHotCells = workingList.OrderBy(c => c.Row * Columns + c.Column).ToList();

            OnPropertyChanged("RaidHotCells");
        }

        private void InitRaidCells()
        {
            RaidHotCells.Clear();
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
            SizeSet = true;
        }

        internal void FirePlayerChanged(string name)
        {
            SizeSet = false;
            PlayerChanged(name);

        }
    }
}
