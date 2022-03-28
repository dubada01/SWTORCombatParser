using SWTORCombatParser.Model.Timers;
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
                OnPropertyChanged();
            }
        }
        public Point TopLeft { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public List<string> OrderedNames = new List<string>();


        public void UpdateNames(List<string> orderedNames)
        {
            OrderedNames = orderedNames;
            OnPropertyChanged("FakeItems");
        }
        public int Rows
        {
            get => rows; set
            {
                rows = value;
                OnPropertyChanged();
                OnPropertyChanged("FakeItems");
            }
        }
        public int Columns
        {
            get => columns; set
            {
                columns = value;
                OnPropertyChanged();
                OnPropertyChanged("FakeItems");
            }
        }

        public List<RaidHotCell> RaidHotCells
        {
            get
            {
                var values = Enumerable.Range(0, (int)(Rows * Columns)).Select(c => new RaidHotCell { ColumnWidth = Width / Columns, RowHeight = Height / Rows, Name = OrderedNames.Count <= c ? "" : OrderedNames[c] }).ToList();
                return values;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void UpdatePositionAndSize(int actualHeight, int actualWidth, Point topLeft)
        {
            TopLeft = topLeft;
            Height = actualHeight;
            Width = actualWidth;
            OnPropertyChanged("FakeItems");
        }
    }
}
